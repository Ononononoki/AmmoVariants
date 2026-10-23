using BepInEx.Configuration;

namespace AmmoVariants
{
    internal static class AmmoTrailSettings
    {
        internal const float PiercingOpacity = AmmoPalette.PiercingOpacity * 0.7f;
        internal const float ScatterOpacity = 0.6f;
        internal const float PiercingWidth = 0.055f * 0.8f;
        internal const float ScatterWidth = 0.044f;
        internal const float ScatterTaper = 0.5f;
        internal const float ScatterHoldShare = 0.7f;
        internal const float ScatterLift = 0.85f;
        internal const float ScatterThickness = 1.5f;
        private static ConfigEntry<bool> scatter;
        private static ConfigEntry<bool> piercing;
        private static ConfigEntry<bool> energy;
        private static ConfigEntry<bool> ballista;

        internal static bool Scatter => scatter?.Value ?? true;
        internal static bool Piercing => piercing?.Value ?? true;
        internal static bool Energy => energy?.Value ?? true;
        internal static bool Ballista => ballista?.Value ?? true;

        internal static void Bind(ConfigFile config)
        {
            const string description = "Show this custom flight trail. Native elemental particles remain. Requires restart.";
            scatter = config.Bind("Trails","Scatter",true,description);
            piercing = config.Bind("Trails","Piercing",true,description);
            energy = config.Bind("Trails","Energy",true,description);
            ballista = config.Bind("Trails","Ballista",true,"Show material-colored trails on existing ballista bolts. Mechanics stay unchanged. Requires restart.");
        }
    }
}
