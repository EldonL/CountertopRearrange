# CountertopRearrange
For the Meta Global AI Hackathon 2026

# Model used: 
- Model: muse-spark-1.3
-Defined in Assets_CountertopRearrange/Scripts/MuseSparkVision.cs:34: public string model = "muse-spark-1.3"
- Called via the Meta Model API at https://api.meta.ai/v1 (Assets_CountertopRearrange/Scripts/MuseSparkVision.cs:47),
 - Responses API, non-streaming. Settings: reasoning.effort = medium, max_output_tokens = 4096, temperature = 1.

# Where model is used
 Where it is used — vision-based placement:
  1. TakeScreenshot captures the AR view, MuseSparkVision.SetTargetObjectName() builds the question via
     Assets_CountertopRearrange/Scripts/SpawnablePrompt.cs:15 ("You are placing a {object} on the countertop... reply
     with exactly one JSON object {"x", "y", "reason"}").
  2. Assets_CountertopRearrange/Scripts/MuseSparkVision.cs:76: uploads PNG to POST /v1/files (purpose user_data), then
     sends POST /v1/responses with input_text + input_image(file_id).
  3. Answer flows through MuseSparkVision.onAnswer → ObjectPlacementController.OnVisionAnswer() → PlacementAnswerParser
     → viewport raycast onto the ARCore counter plane → anchored spawn.

This project let's you point at the counter. ARCore will then recognize the surfaces. You select an object you want to place on the counter. Take a picture, send it to muse, let Muse spark figure out where to put it. Then it returns the answer and your object will spawn in the correct place. 
This works on Android phones. 

1. For the build to work you must enter your own api key. You can enter by going to hierarchy > MuseSparkVision > INspector > MuseSparkVision compoennt > Api Key Override field