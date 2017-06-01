# CoderCards - Class Libary Version

This is a precompiled function version of the Azure Functions sample [CoderCards](https://github.com/lindydonna/codercards).

## About the sample

* This sample uses the new [precompiled function feature](https://github.com/Azure/azure-webjobs-sdk-script/wiki/Precompiled-functions). The project also uses WebJobs attributes instead of `function.json`. Use the `FunctionName` attribute to provide the name that will appear in the portal.

* There are two versions of the project:
   * [CoderCards.csproj](CoderCards/CoderCards.csproj), which requires Visual Studio 2017 Update 3 and the Azure Functions Tools VSIX. When the project is built, the file `function.json` is generated in the build output folder.
   * [CoderCardsWebProj.csproj](CoderCardsWebProj/CoderCardsWebProj.csproj), which is a regular web project. To convert attributes to `function.json`, it runs [Runner.exe](CoderCardsWebProj/build-task/Runner.exe) as a post-build step. To make the project runnable with F5, you must modify the project start action to launch the [Azure Functions Core Tools](https://www.npmjs.com/package/azure-functions-core-tools). (See intructions below.) 

* There are two functions defined in this project:
  * **RequestImageProcessing**. HTTP trigger that writes a queue message. The request payload must be in the following form:

  ```json
      {
        "PersonName": "Scott Guthrie",
        "Title": "Red Polo Connoisseur",
        "BlobName": "Scott Guthrie-Red Polo Connoisseur.jpg"
      }
  ```

  * **GenerateCard**. Queue trigger that binds to the blob specified in the BlobName property of the queue payload. Based on the predominant emotion of the input image, it generates a card using one of 4 card templates.
     
     * The card is written to the output blob container specified by the app setting `output-container`. 

## Setup

There's a Python setup script [setup.py](setup.py) that uses the Azure CLI 2.0 to automate the storage account setup. Run the following commands:

```
az login
python setup.py
```

This will modify the file [local.settings.json](CoderCards/local.settings.json). 

Alternatively, you can run the script from the Azure Cloud Shell in the Azure Portal. Just run `python` and paste the script. The script prints out settings values that you can use to manually modify `local.settings.json`. 

## Required App Settings 

| Key                 | Description |
|-----                | ------|
| AzureWebJobsStorage | Storage account connection string |
| EmotionAPIKey       | Key for [Cognitive Services Emotion API](https://www.microsoft.com/cognitive-services/en-us/emotion-api) |
| input-queue         |  Name of Storage queue for to trigger card generation. Use a value like "local-queue" locally and "input-queue" on Azure
| input-container     | Name of Storage container for input images. Use a value like "local-card-input" locally and "card-input" on Azure |
| output-container     | Name of Storage container for output images. Use a value like "local-card-output" locally and "card-output" on Azure |
| HOME                | Set to "." when running locally. Is automatically set on Azure |
| SITE_PATH           | Use "." when running locally. Use `site\\wwwroot` on Azure |

## Local debugging in Visual Studio 

- If you're using Visual Studio 2017 Update 3 and the Azure Functions Tools VSIX, open the project [CoderCards.csproj](CoderCards/CoderCards.csproj). F5 will automatically launch the Azure Functions Core tools.

- If you're using [CoderCardsWebProj.csproj](CoderCardsWebProj/CoderCardsWebProj.csproj), you must customize the project start action to launch the Azure Functions Core tools. See screenshot below.

    1. Install the [Azure Functions Core Tools](https://www.npmjs.com/package/azure-functions-core-tools) from npm. 
    2. Right-click **CoderCardsWebProj** and open **Properties**. 
    3. In the **Web** tab, choose **Start External Program**
    4. For the program path, enter the path to `func.exe` for the Azure Functions CLI. The path will be similar to like `C:\Users\USERNAME\AppData\Roaming\npm\node_modules\azure-functions-cli\bin\func.exe`
    5. For **Command line arguments** set `host start --cors *`
    6. For Working directory, specify the root of the project `CoderCardsWebProj` on your machine.

  ![Start external program settings](https://cloud.githubusercontent.com/assets/4260261/23055872/1d889b4e-f49d-11e6-9a58-42f42c9d02f3.png)

## Running the demo

### Running using the provided SPA webpage

Make sure the functions host is running locally via Visual Studio or the Azure Functions Core Tools.

In a command prompt, go to the `CoderCardsClient` directory.

- Run `npm install`
- Run `npm start`. This will launch a webpage at `http://127.0.0.1:8080/`. Navigate instead to `http://localhost:8080`.

### Running manually 
1. Choose images that are **square** and upload to the `card-input` container.
2. Send an HTTP request using Postman or CURL, specifying the path of the blob you just uploaded:

    ```json
    {
      "PersonName": "My Name", 
      "Title": "My Title",
      "BlobName": "BlobFilename.jpg"
    }
    ```

## Notes

* The demo uses System.Drawing, which is NOT recommended for production apps. To learn more, see [Why you should not use System\.Drawing from ASP\.NET applications](http://www.asprangers.com/post/2012/03/23/Why-you-should-not-use-SystemDrawing-from-ASPNET-applications.aspx).

* Happy faces get a multiplier of 4, angry gets a multiplier of 2. I encourage you to tweak for maximum comedic effect!

## Talking points about Azure Functions

* Creating an HTTP trigger that writes a queue message is just one line of code!

* Using a queue message to trigger blob processing is preferable to a blob trigger, as it is easier to ensure transactional processing. Also, blob triggers can be delayed for up to 10 minutes on the Consumption plan.

* By binding to a POCO, you can use the payload of a trigger to configure an input binding. In this example, we binding to the `BlobName` property in the queue message.

* The input binding is just a byte array, which makes it easy to manipulate with memory streams (no need to create new ones). Other binding types for C# are Stream, CloudBlockBlob, etc, which is very flexible. The output binding is just a stream that you just write to.
