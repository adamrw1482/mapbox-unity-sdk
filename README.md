## Mapbox Unity SDK 3.1.0 Quickstart guide

For a complete overview of the package — supported Unity versions, build platforms, dependencies, and module breakdown — see [`Documentation~/Overview.md`](Documentation~/Overview.md). For release notes see [`CHANGELOG.md`](CHANGELOG.md).

### Installing the Package

1. **Get the Mapbox Unity SDK v3.1.0**
   - Clone the Mapbox Unity SDK repository to local.

2. **Open Unity Package Manager (UPM)**  
   - Open your Unity project and navigate to the Unity Package Manager.

3. **Add the Package from Disk**  
   - Click the **`Add package from disk`** button in the top-left corner of the UPM window.
   - Locate and select the `package.json` file within the Mapbox folder.

4. **Verify Installation**  
   - The Mapbox Unity SDK package should now appear in the Unity Package Manager window.

---

### Playing the Demo Scenes

1. **Access the Demo Scenes**  
   - The SDK includes two demo scenes:
     - `Location Based Game`
     - `World Map Viewer`

2. **Import the Demo Scenes**  
   - In the Package Manager, select the Mapbox Unity SDK package.
   - Go to the "Samples" tab.
   - Hit the **Import** button to create a `Samples` folder at the root of your project.
   - Unity will copy the demo scene files into this folder.

3. **Configure Your Access Token**  
   - The demo scenes will not work until you set your access token.
   - Open the **Token Configuration** window under the Mapbox section in Unity's top menu.
   - Enter your access token.

4. **Token Storage**  
   - Unity will save the access token in a file located at:  
     `Resources/Mapbox/MapboxConfiguration.txt`

5. **Run the Demo Scenes**  
   - After configuring your access token, open the demo scenes and play them.

---

### Integrating the Location-Based Game Demo Scene into Your Project

The integration process depends heavily on your project setup and how you intend to use the map. Below are steps to help you get started with a location-based game setup:

1. **Import the Location-Based Game Sample**
   - Open the Package Manager, select the Mapbox Unity SDK, go to the **Samples** tab, and hit **Import** next to **LocationBasedGame**.

2. **Use the `LocationBasedMap` Prefab**  
   - Add the `LocationBasedMap` prefab to any scene, and it should work.  
   - Optionally, copy the following to another location in your project for easier customization:
     - `LocationBasedMap` prefab
     - `MapVisuals` folder
     - `Scripts` folder  
   - After copying, you can safely delete the `Samples` folder if it’s no longer needed.

---

### Working with Location

The Mapbox Unity SDK provides two primary methods for working with location:

##### 1. Latitude and Longitude Field in the Map Script

- The **Mapbox Map Behaviour** script includes a field for latitude and longitude.  
- If the `Initialize On Start` checkbox is enabled:
  - The specified latitude and longitude will serve as the starting location of the map.  
  - This method is simple and effective, particularly during development in the Unity editor.  

**Limitations**:  
- This approach does not use the device's location.  
- If you build the project with these settings, the map will display the predefined location specified in the script.

**Usage in the Location-Based Game Demo Scene**:  
- You can move the `CharTarget` game object within the scene. The Astronaut character will walk toward this object in Unity space.


##### 2. Using the LocationModule System

For a more advanced and dynamic solution, you can use the **LocationModule** system. This method is used by default in the Location-Based Game demo scene.

###### Overview:
- The LocationModule system abstracts location handling:  
  - In builds: Uses the device's location.  
  - In the editor: Uses predefined location values.  
- Predefined values can be configured in the **LocationModule** game object located under the map prefab.

###### Steps to Use the LocationModule System:
Step 1. **Disable `Initialize On Start`**  
   - Uncheck the `Initialize On Start` checkbox in the map script.

Step 2. **Add `Snap Map To Location Provider`**  
   - Attach the `Snap Map To Location Provider` script to your map.  
   - Set it up as shown in the provided documentation or image reference.

Step 3. **Enable Player Movement with Device Location**  
   - Attach the `Snap Transform To Location Provider` script to the **`CharTarget`** game object.  
   - Configure the script to update the position of the `CharTarget` object with the device's location in real-time.  
   - The player avatar will automatically follow the `CharTarget` object as it moves.

By following these steps, your map will dynamically update based on the device's location, and the player avatar will respond to real-world movements.

---

###  Building Your Map Application for Android

To build your map application for the Android platform, you'll need specific settings files included in the Mapbox Unity SDK package. Follow these steps:

##### Step 1: Copy Required Files
- Go to the `Mapbox Unity SDK/Runtime/AndroidBuildSettings` folder, you'll find a few folders named after Unity Editor Version.
- Find the one matching (or closest lower) with the version you are using
- Copy the `Plugins` folder inside to your project. Place it under the `Assets` folder.

###### Step 1b: Merging Custom Android Settings (if applicable)
- If your project already contains custom Android settings, merge them with the copied files to ensure compatibility.

##### Step 2: Verify Player Settings
- Open **Project Settings** and navigate to the **Player** section.
- Under the **Publish Settings** section (at the bottom), verify that the custom setting files we just copied are applied.

##### Step 3: Build Your Application
- After completing the above steps, build your application for the Android platform.

