# VirtualPaper legal documents / 法律与隐私说明

The application notices are maintained here as UTF-8 Markdown, one README per supported locale. Each file contains privacy, AI processing and rights, and copyright sections. Document version: **1.0**, updated **2026-11-15**.

应用说明以 UTF-8 Markdown 保存，每种支持语言一份 README，包含隐私政策、AI 处理与权利、版权三部分。文档版本 **1.0**，更新日期 **2026-11-15**。

| Language / 语言 | Document / 文档 |
| --- | --- |
| English | [en-US](en-US/README.md) |
| 简体中文 | [zh-CN](zh-CN/README.md) |
| 繁體中文 | [zh-TW](zh-TW/README.md) |
| 日本語 | [ja-JP](ja-JP/README.md) |
| 한국어 | [ko-KR](ko-KR/README.md) |
| Deutsch | [de-DE](de-DE/README.md) |
| Français | [fr-FR](fr-FR/README.md) |
| Español | [es-ES](es-ES/README.md) |
| Português (Brasil) | [pt-BR](pt-BR/README.md) |
| Русский | [ru-RU](ru-RU/README.md) |

## Maintenance / 维护

- Locale codes follow `localization/LanguageCatalog.json`. UI entry labels remain in `localization/Strings`; the complete legal documents are authored here. Update all translations, document versions and dates together when data handling or rights statements change. Do not translate original third-party licenses in place.
- The three stable section IDs are `privacy-policy`, `ai-processing-and-rights`, and `copyright-and-licenses`. The native offline reader uses these markers. Keep paragraphs on single Markdown lines; headings, paragraphs and Markdown links are sufficient for these documents.
- Copy the locale folders, `THIRD-PARTY.txt`, and `licenses` into `Plugins/UI/Legal` beside the installed UI executable. The UI head's build/publish content feeds both EXE and MSIX. This index is a repository maintenance guide, not the application notice. The application opens the matching language, with the shared English fallback.
- A local file does not replace the Store privacy-policy URL. After the documents reach the public release branch, publish the same content and configure its accessible HTTPS address in Partner Center. For example, the public repository's `doc/legal/en-US/README.md#privacy-policy` can identify the policy. Do not configure an unpublished URL or claim that a local file has already satisfied Store submission.
- The publisher and project maintainer is PaperHammer; GitHub Issues is the current public contact. Keep these details current and direct users to ask for an appropriate channel before providing sensitive information.
- The project maintainer has manually confirmed the third-party sources and redistribution permissions for the current release; this confirmation was recorded on 2026-11-15. Preserve the required notices. When replacing dependencies, model weights or media, check the new items' terms and update the accompanying records.
- The repository's `LICENSE` is the source of truth for the application's GPLv3 text. Keep the installer text and bundled copy identical. Do not silently relicense the application or replace third-party terms with its GPL.

中文：语种跟随统一语言目录；正文与日期同步更新。三个章节锚点用于本地阅读入口。打包仅带用户文档、第三方说明与许可证原文。商店仍需可公开访问的隐私政策 URL，文档合入公开发布分支后再配置。项目现有公开联系方式为 GitHub Issues，敏感信息应先询问合适的提供方式。当前发布内容的第三方来源及再分发授权已由项目维护者人工确认，确认记录日期为 2026-11-15；后续更换依赖、模型权重或素材时，同步核对新条目并更新声明。

## Model provenance checked locally / 本地模型来源核对

Read-only inspection on 2026-11-15 found the AdaIN and Real-ESRGAN conversion scripts and source weights in the owner-provided `onnx` workspace. No script was executed and no original model directory was modified.

- AdaIN: `pytorch-AdaIN/export_onnx.py` loads `models/vgg_normalised.pth` and `models/decoder.pth`; the workspace README points to Naoto Inoue's [v0.0.0 model release](https://github.com/naoto0804/pytorch-AdaIN/releases/tag/v0.0.0). Its original MIT text is retained. The workspace export and application copy match for both files: `adain_style_transfer.onnx` SHA-256 `C3407747A5FD2C54470DDCB6B965215FD80D669F5EE6D6E1516E80DF08452F87`; `.onnx.data` SHA-256 `E5C4DAB6270697A5DB50782831296076A73E67B83679022CBC61DB956CA89BAA`.
- Real-ESRGAN: `realESRGAN/export_onnx.py` loads BasicSR RRDBNet with 23 blocks, scale 4, from `models/RealESRGAN_x4plus.pth` (SHA-256 `4FA0D38905F75AC06EB49A7951B426670021BE3018265FD191D2125DF9D682F1`). The script requests ONNX opset 14 and dynamic spatial dimensions. The supplied workspace contains the checkpoint and script but no retained exported Real-ESRGAN ONNX file. The application's ONNX SHA-256 is `FBAA614169118D529F32F09A943DFD02EA90C97EAA003A3E1EAF689AAD92A1A6`; no re-export or official-checkpoint hash comparison was performed. Keep this evidence boundary when describing provenance.
- Depth: the production depth guide identifies the Small/ViT-S ONNX from Fabio Sim's v2.0.0 conversion release. The current artifact SHA-256 is `46C4E8EEDA3A27F34701831B6A2EC7753D7B38779B215ACB5633424703DEED8F`. Upstream explicitly distinguishes the Small model's Apache-2.0 terms from the larger models' noncommercial terms.

These file and conversion records describe the local inspection separately from the maintainer's licensing confirmation above. Retain the source references, conversion records and artifact hashes when replacing models. A format conversion does not replace the applicable model license.

## References / 参考依据

- [Microsoft Store policies, sections 10.5 and 10.7](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies): privacy-policy URL and product localization.
- [WebView2 data and privacy](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/data-privacy) and the [Microsoft privacy statement](https://privacy.microsoft.com/privacystatement).
- [PRC Copyright Law](https://www.ncac.gov.cn/xxfb/flfg/flfg_532/202103/t20210309_50530.html), especially Articles 3, 11 and 13: originality, authorship and underlying works.
- [U.S. Copyright Office AI report announcement, Part 2](https://www.copyright.gov/newsnet/2025/1060.html): human authorship in AI-assisted works; this is a U.S. interpretation, not a worldwide rule.
- [GNU license translation guidance](https://www.gnu.org/licenses/licenses.en.html#Translations): explanations and translations do not replace the original license terms.
