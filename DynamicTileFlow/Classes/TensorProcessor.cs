using DynamicTileFlow.Classes.JSON;
using DynamicTileFlow.Classes.Servers;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace DynamicTileFlow.Classes
{
    public class TensorProcessor
    {
        public static TensorAPIRequest CreateTensorInput(
            List<Image<Rgba32>> images, 
            int channels, 
            int height, 
            int width)
        {
            int batchSize = images.Count;

            float[,,,] batchInput = new float[batchSize, channels, height, width];

            Parallel.For(0, batchSize, i =>
            {
                PrepareImageDirect(images[i], batchInput, i, height, width);
            });

            float[] flattened = FlattenTensor(batchInput);

            var requestPayload = new TensorAPIRequest
            {
                Inputs = new List<TensorAPIInput>
            {
                new TensorAPIInput
                {
                    Name = "images",
                    Shape = [batchSize, channels, height, width],
                    Datatype = "FP32",
                    Data = flattened
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
        public static void PrepareImageDirect(Image<Rgba32> image, float[,,,] batchInput, int batchIndex, int targetHeight, int targetWidth)
        {
            Parallel.For(0, targetHeight, y =>
            {
                Memory<Rgba32> pixelMemory = image.DangerousGetPixelRowMemory(y);
                Span<Rgba32> pixels = pixelMemory.Span;
                for (int x = 0; x < targetWidth; x++)
                {
                    ref Rgba32 pixel = ref pixels[x];
                    batchInput[batchIndex, 0, y, x] = pixel.R * (1f / 255f);
                    batchInput[batchIndex, 1, y, x] = pixel.G * (1f / 255f);
                    batchInput[batchIndex, 2, y, x] = pixel.B * (1f / 255f);
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
            string[] labels,
            float minConfidence = 0)
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

                    if (maxScore >= minConfidence)
                    {
                        detections.Add(new DetectionResult
                        {
                            X_min = (int)(xCenter - width / 2),
                            Y_min = (int)(yCenter - height / 2),
                            X_max = (int)(xCenter + width / 2),
                            Y_max = (int)(yCenter + height / 2),
                            Label = labels[classId],
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
