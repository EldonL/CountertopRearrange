# Space Rearrange
For the Meta Global AI Hackathon 2026

# Model used: 
- Model: muse-spark-1.3
-Defined in Assets/CountertopRearrange/Scripts/MuseSparkVision.cs:34: public string model = "muse-spark-1.3"
- Called via the Meta Model API at https://api.meta.ai/v1 (Assets_CountertopRearrange/Scripts/MuseSparkVision.cs),
 - Responses API, non-streaming. Settings: reasoning.effort = medium, max_output_tokens = 4096, temperature = 1.

# Where model is used
 Where it is used — vision-based placement:
  1. TakeScreenshot captures the AR view, MuseSparkVision.SetTargetObjectName() builds the question via
     Assets/CountertopRearrange/Scripts/SpawnablePrompt.cs ("You are placing a {object} on the countertop... reply
     with exactly one JSON object {"x", "y", "reason"}").
  2. Assets/CountertopRearrange/Scripts/MuseSparkVision.cs uploads PNG to POST /v1/files (purpose user_data), then
     sends POST /v1/responses with input_text + input_image(file_id).
  3. Answer flows through MuseSparkVision.onAnswer → ObjectPlacementController.OnVisionAnswer() → PlacementAnswerParser
     → viewport raycast onto the ARCore counter plane → anchored spawn.

# Project
What if AI could look at your surroundings and understand exactly where an object belongs?

Our project combines the power of Meta Muse Spark with augmented reality to turn everyday spaces into intelligent environments.

Simply point your camera at a counter. ARCore detects the real-world surface, and you choose an object you want to place. Then, we send an image of your surroundings to Meta Muse Spark, allowing it to analyze the scene, understand the environment, and determine the most appropriate placement for your object.

Muse Spark returns its placement recommendation, and ARCore brings that decision to life by spawning the object in the real world.

This isn't just placing virtual objects in a room. It's giving AI the ability to understand a space and decide how digital objects should interact with it.

Works on Android phones

# Video
https://www.youtube.com/watch?v=2kUAL-EyHI4

# twitter
https://x.com/VPlayuh/status/2116967884727660807

# Other notes

1. Unity version 6000.3.12f1
2. For the build to work you must enter your own api key. You can enter by going to hierarchy > MuseSparkVision > INspector > MuseSparkVision compoennt > Api Key Override field