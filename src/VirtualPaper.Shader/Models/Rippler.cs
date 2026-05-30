namespace VirtualPaper.Shader.Models {
    public struct Rippler {
        public static Rippler Zero = new() {
            Frequency = 140.0f,
            Phase = 0.0f,
            Amplitude = 60.0f,
            Spread = 0.01f,
        };

        public float Frequency;
        public float Phase;
        public float Amplitude;
        public float Spread;

        public Rippler(float time) {
            this.Frequency = 140.0f - time * 7.5f;
            this.Phase = -time * 5.0f;
            this.Amplitude = 60.0f - time * 3.75f;
            this.Spread = 0.01f + time / 40.0f;
        }

        public override readonly string ToString() =>
            $"Frequency:{this.Frequency},Phase:{this.Phase},Amplitude:{this.Amplitude},Spread:{this.Spread}";
    }
}
