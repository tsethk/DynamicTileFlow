# DynamicTileFlow


##Third-Party Libraries

This project uses third-party libraries:

- Newtonsoft.Json (MIT License) – https://github.com/JamesNK/Newtonsoft.Json
- SixLabors.ImageSharp (Apache 2.0 License) – https://github.com/SixLabors/ImageSharp


## Fonts

This project includes the **Roboto** font (Roboto-Regular.ttf) for cross-platform image annotation.  
Roboto is licensed under the **Apache License 2.0**. Full license text is included in `Resources/Roboto-License.txt`.  

Font source: [https://fonts.google.com/specimen/Roboto](https://fonts.google.com/specimen/Roboto)


DynamicTileFlow is maintained by Seth (tsethk@hotmail.com). DynamicTileFlow is licensed under nothing. You are free to use, modify, and distribute it as you see fit.  Just let me know if you are using it and for what, it'll make me happy!

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
- **Annotated image endpoint**: Optional endpoint returns an image with bounding boxes and labels, helpful for testing your dynamic tile configuration.  
- **Tensor query support**: Designed to support advanced AI inference in the future and still integrate with AgentDVR.  

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
                                 │ Load-Balanced │         
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

    /
        This endpoint returns a list of available AI Server endpoints and their descriptions, requests, response times, etc.
        

    /dynamic-tiler
        This endpoint is meant to integrate with AgentDVR and returns JSON in the format AgentDVR expects.

        Form Data:
            Image - FormData - Image File
        Query String:
            TileStrategy (OPTIONAL) - Integer - Indicates which tiling strategy to use from appsetttings.json
                Default: 1


    /dynamic-tiler-image
        This endpoint returns an annotated image optionally with bounding boxes and labels, tile boundaries, or both.

        Form Data:
            Image - FormData - Image File
        Query String:
            TileStrategy (OPTIONAL) - Integer - Indicates which tiling strategy to use from appsetttings.json
                Default: 1
            ResizeRatio (OPTIONAL) - Float - Resize the image result using the ratio provided.
                Default: null (no resizing)  
            IncludeDetections (OPTIONAL) - Boolean - Include bounding boxes for detected objects with labels
                Default: False
            IncludeTiles (OPTIONAL) - Boolean - Include tile boundaries in the output image
                Default: False  


    /simple
        This endpoint skips any tiling plans and sends the entire image to a single AI server (load balancing enabled) and returns the results in AgentDVR format.
        You would probably be better sending it straight to the AI server, but this is here for testing purposes and the eventual inclusion of Tensor servers. 

        Form Data:
            Image - FormData - Image File   



Dynamic Tiling Strategies:

    Tile strategies are defined in appsettings.json. Each strategy can have different tile sizes and overlap settings. The strategy is selected via the TileStrategy query parameter on the endpoints above.
    
    Example appsettings.json:
    
        "DynamicTilePlans": [
            {
                "TilePlanId": 1,                        // Strategy ID passed in TileStrategy query parameter
                "TilePlanName": "Ultra-wide and far",   // Descriptive name for the strategy
                "ImageWidthExpected": 7680,             // Expected width of input images in pixels (not used currently, but for future automatic tile plan selection)
                "ImageHeightExpected": 2160,            // Expected height of input images in pixels (not used currently, but for future automatic tile plan selection) 
                "TilePlans": [                          // List of tile plans to use to split a single image
                    {
                        "Y": 0,                         // The Y coordinate of the image where this tiling will be applied to, tiles will be created in a row from left to right at this Y coordinate
                        "Height": 2160,                 // The height of the tile that will be grabbed from the image
                        "Width": 3840,                  // The width of the tile that will be grabbed from the image
                        "OverlapFactor": 0.25,          // The overlap factor to use for this set of tiles (0.0 to < 0.5)
                        "ScaleWidth": 640               // Scale the width of the tile before sending to AI server (maintains aspect ratio)  
                    },
                    {                                   // Add as many tile plans as needed for different areas of the image, they will all be processed and aggregated for final results using the NMS routines
                        "Y": 0,
                        "Height": 2160,
                        "Width": 1920,
                        "OverlapFactor": 0.25,
                        "ScaleWidth": 640
                    },
                    {                                   // For this plan the very top of the image will be examined in 640x640 tiles with 20% overlap
                      "Y": 0,
                      "Height": 640,
                      "Width": 640,
                      "OverlapFactor": 0.2,
                      "ScaleWidth": 640
                    },
                    {                                   // Then the next 640 pixels down will be examined in 640x640 tiles with 20% overlap (hence the starting Y of 512 instead of 640)
                      "Y": 512,
                      "Height": 640,
                      "Width": 640,
                      "OverlapFactor": 0.2,
                      "ScaleWidth": 640
                    }
                    ...                                 // Add more tile plans as needed
                ]
            },
            {                                           // Add multiple tile strategies and use the TileStrategy query parameter to select per camera in AgentDVR
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
    The load is determined by the number of active requests and the average response time of each server. If a server fails to return a response
    within the timeout period it is marked as inactive and will not receive new requests until a process checks the base URL for the server and
    sees that it is active.  This check is done every 30 seconds in a separate thread as requests come in, so if no requests are received the server will stay inactive
    indefinitely.   
    
    Example appsettings.json:


        "AIServers": [
            {
                "Endpoint": "/v1/vision/detection",     // The endpoint on the AI server to send detection requests to
                "ServerName": "192.168.247.170",        // The IP address or hostname of the AI server
                "Port": 32168,                          // The port of the AI server
                "Name": "AI",                           // A friendly name for the server
                "Type": "CodeProjectAI",                // The type of AI server (currently only CodeProjectAI is supported, Tensor is partially implemented and working in some cases)
                "TimeoutInSeconds": 2,                  // Timeout for requests to this server   
                "IsSSL": false,                         // Indicate if this server uses SSL (https) 
                "MovingAverageAlpha": 0.1               // Alpha value for calculating moving average of response times (0.0 to 1.0) - lower values give more weight to older requests
            },
            {                                           // You can add multiple AI servers for load balancing, or only a single if you only have one
                "Endpoint": "/v1/vision/detection",
                "ServerName": "192.168.148.66",
                "Port": 32168,
                "Name": "JF",
                "Type": "CodeProjectAI",
                "TimeoutInSeconds": 2,
                "IsSSL": false,                         
                "MovingAverageAlpha": 0.1               
            },
            ...
        ]

        