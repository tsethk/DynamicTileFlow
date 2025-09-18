namespace DynamicTileFlow.Classes.JSON
{
    public class TensorAPIInput
    {
        public string? Name { get; set; }
        public string? Datatype { get; set; }
        public int[]? Shape { get; set; }
        public float[]? Data { get; set; }
    }
}
