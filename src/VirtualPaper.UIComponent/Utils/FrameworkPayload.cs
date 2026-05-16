using System;
using System.Collections.Generic;

namespace VirtualPaper.UIComponent.Utils {
    public sealed class FrameworkPayload {
        public object? this[string key] {
            set => Set(key, value);
        }

        public object? this[NaviPayloadKey key] {
            set => Set(key.ToString(), value);
        }

        public void Set(string key, object? value) {
            _data[key] = value;
        }

        public void Set(NaviPayloadKey key, object? value) {
            Set(key.ToString(), value);
        }

        public bool TryGet<T>(string key, out T value) {
            if (_data.TryGetValue(key, out var obj) && obj is T t) {
                value = t;
                return true;
            }

            value = default!;
            return false;
        }

        public bool TryGet<T>(NaviPayloadKey key, out T value) {
            return TryGet(key.ToString(), out value);
        }

        public T? Get<T>(string key) {
            if (_data.TryGetValue(key, out var value) && value is T t)
                return t;

#if DEBUG
            GlobalMessageUtil.ShowWarning($"[DEBUG] FrameworkPayload missing required key '{key}' ({typeof(T).Name})");
#endif
            return default;
        }
        
        public T? Get<T>(NaviPayloadKey key) {
            return Get<T>(key.ToString());
        }

        public bool ContainsKey(string key) {
            return _data.ContainsKey(key);
        }

        public bool ContainsKey(NaviPayloadKey key) {
            return _data.ContainsKey(key.ToString());
        }

        public IReadOnlyDictionary<string, object?> GetRawData() {
            return _data;
        }

        public void Remove(NaviPayloadKey draftConfigTCS) {
            if (ContainsKey(draftConfigTCS)) {
                _data.Remove(draftConfigTCS.ToString());
            }
        }

        private readonly Dictionary<string, object?> _data = [];
    }

    public static class NavigationPayloadExtensions {
        public static FrameworkPayload AddRange(this FrameworkPayload payload, params NaviPayloadData[] items) {
            return payload.AddRange(true, items);
        }

        public static FrameworkPayload AddRange(this FrameworkPayload payload, bool overwrite, params NaviPayloadData[] items) {
            if (items is null) return payload;

            foreach (var item in items) {
                string keyStr = item.Key.ToString();
                if (!overwrite && payload.ContainsKey(keyStr)) {
                    continue;
                }
                payload.Set(keyStr, item.Value);
            }

            return payload;
        }

        public static NaviPayloadData[] ToArray(this FrameworkPayload payload) {
            if (payload == null) return [];

            var list = new List<NaviPayloadData>();
            var rawData = payload.GetRawData();

            foreach (var kvp in rawData) {
                if (Enum.TryParse<NaviPayloadKey>(kvp.Key, out var enumKey)) {
                    list.Add(new NaviPayloadData(enumKey, kvp.Value!));
                }
            }

            return list.ToArray();
        }

        public static FrameworkPayload Merge(this FrameworkPayload? target, FrameworkPayload? source, bool overwrite = true) {
            if (target is null) return source ?? new();
            if (source is null) return target;

            foreach (var kvp in source.GetRawData()) {
                if (!overwrite && target.ContainsKey(kvp.Key)) {
                    continue;
                }
                target.Set(kvp.Key, kvp.Value);
            }

            return target;
        }
    }

    public record NaviPayloadData(NaviPayloadKey Key, object Value);

    public enum NaviPayloadKey {
        OnlyDetails,
        PreviewWithWeb,
        IEffectService,
        StartArgs,
        AvailableConfigTab,
        IWpBasicData,
        IIpcObserver,
        ArcWindow,
        ApplyService,
        ConfigSpacePage,
        RecentUsedFiles,
        LocalFiles,
        DraftPage,
        Project,
        ICardComponent,
        INavigateComponent,
        ServiceProvider,
        InkProjectSession,
        ArcPageContext,
        TargetDraftPanelState,
        IsFromWorkSpace_AddProj,
        DraftConfigPreBtnAction,
        DraftConfigNxtBtnAction,
        DraftConfigTCS,
        StaticImgFileName,
    }
}
