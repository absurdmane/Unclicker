namespace Unclicker
{
    internal enum StationKind
    {
        None,
        Growing,
        Manufacturing,
        Process
    }

    internal static class Stations
    {
        /// <summary>The station kind if it's enabled in the config, otherwise None.</summary>
        public static StationKind KindOf(CraftingBase station)
        {
            switch (station)
            {
                case Growing _ when Plugin.Growing.Value:
                    return StationKind.Growing;
                case manufacturingProcess _ when Plugin.ManufacturingProcess.Value:
                    return StationKind.Process;
                case manufacturing m when Plugin.Manufacturing.Value && !m.Autocraft:
                    return StationKind.Manufacturing;
                default:
                    return StationKind.None;
            }
        }
    }
}
