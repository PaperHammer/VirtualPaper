namespace VirtualPaper.Common.Models
{
    public class NowPlayingEventArgs : EventArgs
    {
        public string AlbumArtist { get; set; } = string.Empty;
        public string AlbumTitle { get; set; } = string.Empty;
        public int AlbumTrackCount { get; set; }
        public string Artist { get; set; } = string.Empty;
        public List<string> Genres { get; set; } = [];
        public string PlaybackType { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Thumbnail { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int TrackNumber { get; set; }
        //public ColorProperties Colors { get; set; }
    }
}
