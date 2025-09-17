using DynamicTileFlow.Classes.Servers;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace DynamicTileFlow.Classes
{
    public class TensorProcessor
    {
        public static TensorAPIRequest CreateTensorInput(List<Image<Rgba32>> Images, int Channels, int Height, int Width)
        {
            int BatchSize = Images.Count;

            float[,,,] batchInput = new float[BatchSize, Channels, Height, Width];

            Parallel.For(0, BatchSize, i =>
            {
                PrepareImageDirect(Images[i], batchInput, i, Height, Width);
            });

            float[] flattened = FlattenTensor(batchInput);

            var requestPayload = new TensorAPIRequest
            {
                inputs = new List<TensorAPIInput>
            {
                new TensorAPIInput
                {
                    name = "images",
                    shape = [BatchSize, Channels, Height, Width],
                    datatype = "FP32",
                    data = flattened
                }
            }
            };

            return requestPayload;
        }
        public static float[] FlattenTensor(float[,,,] tensor)
        {
            int batch = tensor.GetLength(0);
            int channels = tensor.GetLength(1);
            int height = tensor.GetLength(2);
            int width = tensor.GetLength(3);

            float[] flat = new float[batch * channels * height * width];

            int channelsHeightWidth = channels * height * width;
            int heightWidth = height * width;

            int total = batch * channels * height * width;
            Parallel.For(0, total, i =>
            {
                int w = i % width;
                int h = (i / width) % height;
                int c = (i / (width * height)) % channels;
                int b = i / (width * height * channels);
                flat[i] = tensor[b, c, h, w];
            });

            return flat;
        }
        public static void PrepareImageDirect(Image<Rgba32> Image, float[,,,] BatchInput, int BatchIndex, int TargetHeight, int TargetWidth)
        {
            Parallel.For(0, TargetHeight, y =>
            {
                Memory<Rgba32> pixelMemory = Image.DangerousGetPixelRowMemory(y);
                Span<Rgba32> pixels = pixelMemory.Span;
                for (int x = 0; x < TargetWidth; x++)
                {
                    ref Rgba32 pixel = ref pixels[x];
                    BatchInput[BatchIndex, 0, y, x] = pixel.R * (1f / 255f);
                    BatchInput[BatchIndex, 1, y, x] = pixel.G * (1f / 255f);
                    BatchInput[BatchIndex, 2, y, x] = pixel.B * (1f / 255f);
                }
            });
        }

        public static async Task<TensorAPIResponse?> DecodeTensorApiResponse(HttpResponseMessage? response)
        {
            if (response != null)
            {
                var json = await response.Content.ReadAsStringAsync();
                var parsed = JsonConvert.DeserializeObject<TensorAPIResponse>(json);
                return parsed;
            }
            else
            {
                return null;
            }
        }
        public static List<DetectionResult> DecodeDetections(
            float[] data, 
            int batchSize, 
            int features, 
            int numBoxes, 
            float confThreshold, 
            string[] Labels,
            float MinConfidence = 0)
        {
            List<DetectionResult> detections = new List<DetectionResult>();

            for (int batch = 0; batch < batchSize; batch++)
            {

                for (int box = 0; box < numBoxes; box++)
                {
                    float xCenter = data[batch * features * numBoxes + 0 * numBoxes + box];
                    float yCenter = data[batch * features * numBoxes + 1 * numBoxes + box];
                    float width = data[batch * features * numBoxes + 2 * numBoxes + box];
                    float height = data[batch * features * numBoxes + 3 * numBoxes + box];


                    // Class probabilities start at feature 4 up to feature 83 (80 classes)
                    // Find class with max score
                    int classId = -1;
                    float maxScore = float.MinValue;
                    for (int c = 4; c < features; c++)
                    {
                        float score = data[batch * features * numBoxes + c * numBoxes + box];
                        if (score > maxScore)
                        {
                            maxScore = score;
                            classId = c - 4; // adjust index to start at 0
                        }
                    }

                    if (maxScore >= MinConfidence)
                    {
                        detections.Add(new DetectionResult
                        {
                            x_min = (int)(xCenter - width / 2),
                            y_min = (int)(yCenter - height / 2),
                            x_max = (int)(xCenter + width / 2),
                            y_max = (int)(yCenter + height / 2),
                            Label = Labels[classId],
                            Confidence = maxScore,
                            BatchNumber = batch,
                        });
                    }
                }
            }

            return detections;
        }
    }
}
