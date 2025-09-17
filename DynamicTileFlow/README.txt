# DynamicTileFlow

This project uses third-party libraries:

- Newtonsoft.Json (MIT License) – https://github.com/JamesNK/Newtonsoft.Json
- SixLabors.ImageSharp (Apache 2.0 License) – https://github.com/SixLabors/ImageSharp



**DynamicTileFlow** is a **dynamic image processing API** designed to integrate with **AgentDVR** as a CodeProject.AI endpoint. AgentDVR can send image or frame requests to DynamicTileFlow, which will:

- Split large images into smaller tiles based on configurable tile plans.
- Send each tile to the AI server with the **least load**.
- Aggregate object detection results into a single output.
- Return results either in **AgentDVR-compatible JSON** or as a **single annotated image**.
- Support labeled bounding boxes and object detection overlays.
- Can extend to tensor-based AI queries. (Needs work)

DynamicTileFlow acts as a **single API endpoint**, letting AgentDVR focus on capturing video while DynamicTileFlow handles AI inference behind the scenes.

---

## Features

- **Dynamic tiling**: Automatically splits large images for efficient AI processing.  
- **Load-balanced AI server routing**: Tiles are sent to the least-loaded AI server dynamically.  
- **AgentDVR integration**: Returns JSON in the format AgentDVR expects.  
- **Annotated image endpoint**: Optional endpoint returns an image with bounding boxes and labels.  
- **Tensor query support**: Designed to support advanced AI inference in the future.  

---


Simple Diagram of Flow: 
                                 ┌───────────────┐
                                 │   AgentDVR    │
                                 │               │
                                 └──────┬────────┘
                                        │ POST image/frame through normal AI Object Detection API
                                        ▼
                               ┌───────────────────┐
                               │ DynamicTileFlow   │
                               │   API Endpoint    │
                               └────────┬──────────┘
                                        │                  
                                        ▼                  
                                 ┌───────────────┐         
                                 │ Tile & Split  │         
                                 │ Large Images  │         
                                 └───────┬───────┘         
                                         │                 
                                         ▼                 
                                 ┌───────────────┐         
                                 │ Load-Balance  │         
                                 │ AI Servers    │         
                                 └───────┬───────┘         
                                         │                 
                                         ▼                 
                                 ┌───────────────┐         
                                 │ AI Server(s)  │         
                                 │ Detection     │         
                                 └───────┬───────┘         
                                         │                 
                                         ▼                 
                                 ┌───────────────┐                     
                                 │ Aggregate     │                     
                                 │ Detection     │                     
                                 │ Results       │                     
                                 └───────┬───────┘                     
                                         │                             
                                         ▼                             
                                 ┌───────────────┐                     
                                 │ Return JSON   │
                                 │ (AgentDVR     │
                                 │ format)       │
                                 └───────────────┘


Endpoints:

    /dynamic-tiler-image
        This endpoint returns an annotated image optionally with bounding boxes and labels, tile boundaries, or both.

        Form Data:
            Image - FormData - Image File
        Query String:
            TileStrategy (REQUIRED, default = 1) - Integer - Indicates which tiling strategy to use from appsetttings.json
            ResizeRatio (OPTIONAL) - Float - Resize the image result using the ratio provided.
            IncludeDetections (OPTIONAL) - Boolean - Include bounding boxes for detected objects with labels
            IncludeTiles (OPTIONAL) - Boolean - Include tile boundaries in the output image


    /dynamic-tiler
        This endpoint is meant to integrate with AgentDVR and returns JSON in the format AgentDVR expects.

        Form Data:
            Image - FormData - Image File
        Query String:
            TileStrategy (REQUIRED, default = 1) - Integer - Indicates which tiling strategy to use from appsetttings.json



    /simple
        This endpoing is a simplified version of /dynamic-tiler that does not use tiling. It sends the entire image to a single AI server and returns the results in AgentDVR format.

        Form Data:
            Image - FormData - Image File   



Dynamic Tiling Strategies:

    Tile strategies are defined in appsettings.json. Each strategy can have different tile sizes and overlap settings. The strategy is selected via the TileStrategy query parameter.
    
    Example appsettings.json:
    
        "DynamicTilePlans": [
            {
                "TilePlanId": 1,                        // Strategy ID passed in TileStrategy query parameter
                "TilePlanName": "Ultra-wide and far",   // Descriptive name for the strategy
                "ImageWidthExpected": 7680,             // Expected width of input images in pixels (not used currently, but for future automatic tile plan selection)
                "ImageHeightExpected": 2160,            // Expected height of input images in pixels (not used currently, but for future automatic tile plan selection) 
                "TilePlans": [                          // List of tile plans to use to split a single image
                    {
                        "Y": 0,                         //The Y coordinate of the image where this tiling will be applied to
                        "Height": 2160,                 //The height of the tile that will be grabbed from the image
                        "Width": 3840,                  //The width of the tile that will be grabbed from the image
                        "OverlapFactor": 0.25,          //The overlap factor to use for this set of tiles (0.0 to 0.5)
                        "ScaleWidth": 640               //Scale the width of the tile before sending to AI server (maintains aspect ratio)  
                    },
                    {                                   //Add as many tile plans as needed for different areas of the image, they will all be processed and aggregated for final results
                        "Y": 0,
                        "Height": 2160,
                        "Width": 1920,
                        "OverlapFactor": 0.25,
                        "ScaleWidth": 640
                    },
                    ...
                ]
            },
            {                                           //Add multiple tile strategies and use the TileStrategy query parameter to select per camera in AgentDVR
                "TilePlanId": 2,
                "TilePlanName": "Test",
                "ImageWidthExpected": 640,
                "ImageHeightExpected": 640,
                "TilePlans": [
                    {
                        "Y": 0,
                        "Height": 640,
                        "Width": 640,
                        "OverlapFactor": 0.0,
                        "ScaleWidth": 640
                    },
                    ...
                ]
            },
            ...
        ]

AI Servers:
    If you have multiple AI servers, you can define them in appsettings.json. DynamicTileFlow will send tiles to the server with the least load. 
    The load is determined by the number of active requests and the average response time of each server.
    
    Example appsettings.json:


        "AIServers": [
            {
                "Endpoint": "/v1/vision/detection",     // The endpoint on the AI server to send detection requests to
                "ServerName": "192.168.247.170",        // The IP address or hostname of the AI server
                "Port": 32168,                          // The port of the AI server
                "Name": "AI",                           // A friendly name for the server
                "Type": "CodeProjectAI",                // The type of AI server (currently only CodeProjectAI is supported, Tensor is partially implemented and working in some cases)
                "TimeoutInSeconds": 2                   // Timeout for requests to this server   
            },
            {                                           // You can add multiple AI servers for load balancing, or only a single if you only have one
                "Endpoint": "/v1/vision/detection",
                "ServerName": "192.168.148.66",
                "Port": 32168,
                "Name": "JF",
                "Type": "CodeProjectAI",
                "TimeoutInSeconds": 2
            },
            ...
        ]