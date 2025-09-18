using DynamicTileFlow.Classes.Servers;

namespace DynamicTileFlow.Classes.JSON
{
    public class TensorAPIResponse
    {
        public string? Model_name { get; set; }
        public string? Model_version { get; set; }
        public TensorAPIOutput[]? Outputs { get; set; }
    }
}
