# 手动模型精度评估 / Manual precision probe

这是独立的 Windows x64 无界面实验工具，不加入解决方案、MSTest 清单或 CI 门禁。它直接加载现有 Release 产物的原生 ONNX Runtime，比较 Real-ESRGAN 原图和转换图，不启动 VirtualPaper、安装程序或真实 UI，不操作键鼠。

The probe runs real model graphs in isolated console processes. It is a manual measurement, not application or installed acceptance. Generated models, input/output tensors and traces stay under ignored `TestResults` directories.

## 前置条件

- 先按[测试指南](../README.zh-CN.md)构建完整 Release 解决方案；本工具不构建应用依赖。需要 UI payload 中的托管 ORT，以及 UI、ML.Test 各自的原生 ORT/DirectML 文件。
- 使用 .NET 8 SDK。机器同时装有更新 SDK 时，在结果目录的 `build/global.json` 中选定本机已安装的 8.x SDK，并从该目录执行构建；不要修改产品 SDK 配置。`OrtManagedPath` 可指定托管 DLL 的其他位置。
- Python 3.11；本轮转换使用 `numpy==1.26.4`、`onnx==1.17.0`、`onnxconverter-common==1.14.0`，已有环境还提供 `protobuf==5.29.1`、`Pillow==11.0.0`、`psutil==6.0.0`。转换器 1.14 不兼容 NumPy 2.4 的二进制 `fromstring` 接口。可以安装到隔离目录，并通过 `prepare.py --packages <目录>` 加载转换依赖；`measure.py`、`summarize.py` 仍使用所选 Python 环境中的 psutil/NumPy。
- 模型源文件位于 `src/VirtualPaper.ML/*/ai_models`；AdaIN 的 `.onnx.data` 也必须存在。这里只转换超分模型，另外两类仅登记元数据，不代表已验证它们的半精度兼容性。
- DirectML 可用的 GPU。测量会占用计算资源和显存；避免同时进行其他 GPU/推理任务。进程专用/共享显存通过 Windows `GPU Process Memory` 性能计数器采样，权限或计数器不可用时不能声称完成显存比较。

## 执行

从仓库根目录运行，准备一个全新的结果目录：

```powershell
$probeRoot = Join-Path $PWD "TestResults/model-precision-$([guid]::NewGuid().ToString('N'))"
$probeProject = Join-Path $PWD 'test/InferencePrecision/InferencePrecision.csproj'
New-Item -ItemType Directory -Path $probeRoot | Out-Null

# 使用 .NET 8 SDK；此构建命令可从固定该 SDK 的 build 目录执行，之后回到仓库根目录。
dotnet build $probeProject -c Release

# 在已有所需 Python 基础依赖的环境中，隔离转换器和 NumPy：
python -m pip install --no-deps --target "$probeRoot/python" numpy==1.26.4 onnx==1.17.0 onnxconverter-common==1.14.0
python test/InferencePrecision/prepare.py $probeRoot --packages "$probeRoot/python"
python test/InferencePrecision/measure.py $probeRoot --phase smoke
python test/InferencePrecision/measure.py $probeRoot --phase timing
python test/InferencePrecision/measure.py $probeRoot --phase profile
python test/InferencePrecision/summarize.py $probeRoot
```

阶段顺序执行，避免互相争抢 GPU/CPU。任一阶段都保留失败输出；不要在同一目录重跑覆盖证据。`measure.py` 会继续记录不兼容的组合，必须查看每项 `Success` 和进程退出码，最后由 `summarize.py` 检查完整清单、执行结果、有限数值、输出尺寸、GPU 执行证据及模型/输入哈希。超时或缺少结果也会使汇总失败。

## 测量边界

- `prepare.py` 使用 ONNX 官方建议的转换器，保留 float 输入输出接口，记录权重类型、算子、opset、转换参数和 SHA-256。默认保留部分不支持的算子为 float，不保证整张图都是半精度。原模型从不覆盖。
- `smoke`：两套真实原生 ORT 各自执行 32×32 原图 GPU、半精度 GPU、半精度 CPU；小尺寸只检查执行兼容性。
- `timing`：生产单个 512×512 RGB 分块 → 2048×2048 输出，合成图和现有照片各两轮，按 FP32/FP16、FP16/FP32 顺序交替；每进程记录会话创建、首次运行和 3 次热运行。CPU 两种精度各测一进程作参考，不用于宣称两种精度的 CPU 性能差异显著。
- `profile`：独立执行两种图的 512×512 GPU 推理，核对实际算子 provider 和类型；另外用小尺寸查看 CPU 执行半精度图的转换。profiling 关闭的样本才用于性能比较。
- 每进程只处理一种输入；导出张量前冻结工作集/显存采样，避免将导出复制算作模型内存。采样间隔 100 ms 可能漏掉瞬时峰值；进程工作集不等于全部系统内存，GPU 专用显存也不等于全卡占用。
- 新进程会话初始化不是清空驱动/文件缓存的冷启动；首次兼容性运行可能明显慢于后续测量。保留全部原始记录，不能只挑最快的一次。
- 对比的是单个分块的推理调用，含同步输出；不包含应用分块、边缘填充、拼接、缩放和文件 I/O，也未测整个进程组。数值差异以 FP32 输出为参考，不是有真实标注的质量评分，更不是无损承诺。

## 结果与后续

原始证据为 `manifest.json`、每项 `request.json/result.json/monitor.json`、执行轨迹和原始 float 张量，汇总为 `summary.json`。硬件、驱动和每个原生 DLL 的版本/哈希也应与测量一起保存。本轮结论见[超分精度评估](ASSESSMENT.zh-CN.md)。

这不是发布模型的入口。新增派生模型前，需要核实原 ONNX 权重的导出来源、上游权重与许可，并随分发保留必要声明；不得只凭上游代码仓库的许可推定任意下载权重可分发。

参考：[ONNX float16 转换](https://onnxruntime.ai/docs/performance/model-optimizations/float16.html)、[DirectML 执行要求](https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html)。
