namespace VirtualPaper.Common.Runtime.PlayerWeb {
    public static class PlayingFileWeb {
        public static string PlayerWeb3D => _playerWeb3D;
        public static string PlayerWeb => _playerWeb;

        private static readonly string _playerWeb3D = Path.Combine("PLAYER_Web", "3d_depth_map.html");
        private static readonly string _playerWeb = Path.Combine("PLAYER_Web", "default.html");
    }

    [Flags]
    public enum DataConfigTab {
        None = 0,
        GeneralEffect = 1,
        GeneralInfo = 2,
        GeneralInfoEdit = 4,
    }
}
