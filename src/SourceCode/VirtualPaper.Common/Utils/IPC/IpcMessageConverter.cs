using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VirtualPaper.Common.Utils.IPC
{
    public class IpcMessageConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IpcMessage);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);
            return (MessageType)jo["Type"].Value<int>() switch
            {
                MessageType.cmd_apply => jo.ToObject<VirtualPaperApplyCmd>(serializer),
                MessageType.cmd_initFilter => jo.ToObject<VirtualPaperInitFilterCmd>(serializer),
                MessageType.cmd_reload => jo.ToObject<VirtualPaperReloadCmd>(serializer),
                MessageType.cmd_close => jo.ToObject<VirtualPaperCloseCmd>(serializer),
                MessageType.cmd_screenshot => jo.ToObject<VirtualPaperScreenshotCmd>(serializer),
                MessageType.cmd_suspend => jo.ToObject<VirtualPaperSuspendCmd>(serializer),
                MessageType.cmd_resume => jo.ToObject<VirtualPaperResumeCmd>(serializer),
                MessageType.cmd_muted => jo.ToObject<VirtualPaperMuted>(serializer),
                //MessageType.cmd_volume => jo.ToObject<VirtualPaperVolumeCmd>(serializer),
                //MessageType.lsp_perfcntr => jo.ToObject<VirtualPaperSystemInformation>(serializer),
                //MessageType.lsp_nowplaying => jo.ToObject<VirtualPaperSystemNowPlaying>(serializer),
                MessageType.vp_slider => jo.ToObject<VirtualPaperSlider>(serializer),
                MessageType.vp_textbox => jo.ToObject<VirtualPaperTextBox>(serializer),
                MessageType.vp_dropdown => jo.ToObject<VirtualPaperDropdown>(serializer),
                //MessageType.vp_fdropdown => jo.ToObject<VirtualPaperFolderDropdown>(serializer),
                MessageType.vp_button => jo.ToObject<VirtualPaperButton>(serializer),
                MessageType.vp_cpicker => jo.ToObject<VirtualPaperColorPicker>(serializer),
                MessageType.vp_chekbox => jo.ToObject<VirtualPaperCheckbox>(serializer),
                MessageType.msg_console => jo.ToObject<VirtualPaperMessageConsole>(serializer),
                MessageType.msg_hwnd => jo.ToObject<VirtualPaperMessageHwnd>(serializer),
                MessageType.msg_screenshot => jo.ToObject<VirtualPaperMessageScreenshot>(serializer),
                MessageType.msg_wploaded => jo.ToObject<VirtualPaperMessageWallpaperLoaded>(serializer),
                _ => null,
            };
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
