iOS External Input README

Thank you for downloading the iOS External Input asset. This asset is designed to allow Unity iOS/tvOS developers to quickly add support for external Bluetooth devices like keyboards, accessible switches, and more! 

The asset provides a barebones example scene and prefab ExternalInputController object. Building the example scene and deploying to an iOS or tvOS device with a connected Bluetooth accessory capable of transmitting keystrokes will log all inputted keystrokes to the console via the plugin's ExternalInputController class. How you respond to the received keystroke events is up to you!

To add the asset's capabilities to your project, simply import the asset's Plugins and Prefabs folders into your project, and drop in the ExternalInputController prefab into your scene.

Once imported into your project, you can register an event listener in any MonoBehaviour to respond to KeyCodes as they arrive, using the following convention:

----------

void Start() {
    ExternalInputController.instance.OnExternalInputDidReceiveKey += HandleExternalInput;
}

void HandleExternalInput(KeyCode receivedKeyCode)
{
    // respond to received KeyCodes here
}

----------

The plugin's native code also offers a log function to help with further expanding the supported set of key codes / characters for non-English characters and other symbols.

This asset is provided free of charge and is open source. If you use the asset in your project, please acknowledge so in the credits and consider leaving a positive review on the Asset Store.

If you have any questions, please contact me at alexander@hodge.io and I'd be happy to assist!

Alexander Hodge
http://www.hodge.io