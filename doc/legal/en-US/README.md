# VirtualPaper: privacy, AI processing and copyright

Document version: 1.0. Last updated: 2026-11-15. Publisher and project maintainer: PaperHammer.

These notices explain how the EXE and Microsoft Store editions of VirtualPaper handle data, and what to consider when using AI features and bundled content. External websites and services have their own policies.

<a id="privacy-policy"></a>
## Privacy policy

### Local files and settings

VirtualPaper reads the pictures, videos, animated images and Web projects you select so that it can display, edit or process them. Wallpaper information, thumbnails, favorites, tags, effect presets, recent project paths, settings and monitor layouts are stored on your computer. You do not need a VirtualPaper account. The application does not upload these materials or records to PaperHammer for AI processing, advertising or behavioral analytics.

The default data folder is `%LOCALAPPDATA%\VirtualPaper`. For packaged editions, Windows may redirect data to another local location. Wallpaper libraries use the location selected in Settings; exported projects and images are saved where you choose. Previews, undo history, depth maps and browser caches may create additional local files. These files are not automatically backed up to the cloud.

### Diagnostics and reports

Local logs can contain application and Windows versions, interface language, CPU/GPU information, errors, file paths and playback details. Exporting logs creates a local ZIP containing logs, user settings and wallpaper layouts. It is not automatically sent to PaperHammer. Review the archive before sharing it: paths can disclose your Windows user name or private project names. Reports and attachments you voluntarily post to GitHub are handled by GitHub and may be public.

### Location

When you enable Time Perception, the application requests Windows location access and calculates sunrise and sunset on your computer using latitude and longitude. If your location is unavailable, it uses a default location, so lighting changes may not match your area. Turn off Time Perception to stop its location requests. You can also manage permission in Windows Privacy & security settings. The coordinates used for these calculations are not sent to PaperHammer.

### Network requests and third parties

The EXE edition can contact GitHub and download hosts to check for and obtain application or plugin updates. Installation may download Microsoft runtime prerequisites. Store delivery and updates are handled by Microsoft. A request exposes connection information, such as your IP address and request headers, to its recipient. Opening help, feedback or source links connects to the selected website. Web wallpapers and imported Web projects may independently load remote scripts, media or services under their authors' control.

WebView2 diagnostics and SmartScreen send information to Microsoft; see the [Microsoft privacy statement](https://privacy.microsoft.com/privacystatement). Some reporting follows Windows diagnostic settings. These component behaviors are separate from VirtualPaper's local AI processing.

### Retention, security and your choices

Saved settings, libraries, projects, depth-map caches and browser profiles stay on your computer until you delete them or the application removes them during cleanup or uninstallation. Temporary AI and editor results may be cleared the next time the application starts; save them or add them to your library if you want to keep them. Some logs and caches have no fixed automatic deletion schedule. Libraries moved to other locations and exported files may remain after uninstallation.

You can inspect, export, correct or delete your local files and change the relevant settings. Before manually removing application data, exit VirtualPaper and its background processes and back up files you want to keep. Local data relies on Windows account and filesystem protections; VirtualPaper does not provide a separate encrypted vault. Choose trusted wallpaper projects and protect your Windows account.

### Contact and changes

For privacy questions or requests about information you have voluntarily shared with the project, contact PaperHammer through [GitHub Issues](https://github.com/PaperHammer/VirtualPaper/issues). Do not post sensitive information publicly; first request an appropriate way to provide it. Information held by GitHub, Microsoft or a wallpaper's external service is subject to that provider's controls. This notice will be updated when relevant application behavior changes; its version and date identify the copy supplied with your application.

<a id="ai-processing-and-rights"></a>
## AI processing and rights

Depth estimation, style transfer and image enhancement use models on your computer, with CPU/GPU execution depending on the feature and settings. These inference operations do not call a cloud AI service. Model files must be available locally. Output quality and speed depend on the source, model and hardware; generated detail can be inaccurate, and a depth map is an estimate rather than a measurement.

Processing material with AI does not, by itself, give you copyright in the source or result, or permission to use someone else's material. Whether a result qualifies for copyright protection, and who holds those rights, depends on applicable law, the source material's license and human creative contributions. Existing restrictions do not automatically disappear after AI processing. Before publishing, redistributing or using a result commercially, make sure you have the necessary rights or permissions. Copyright, likeness, trademark and other third-party rights may still apply.

VirtualPaper does not claim ownership of your inputs or results merely because you use the application. The software's open-source license does not automatically license your materials or outputs under the GPL. Model/runtime licenses and source-image permissions are separate; a freely available model is not a guarantee that every resulting image is unrestricted. VirtualPaper does not guarantee non-infringement, exclusivity or suitability for commercial use. This notice does not replace permissions or limit rights that applicable law gives you.

The project used OpenAI image generation to create some of the bundled landscape wallpapers. Each wallpaper's accompanying notice describes its source and terms of use. The project claims no exclusive copyright in those AI-generated images. Preparing these bundled assets is separate from the application's local AI processing; it does not cause your images to be sent to OpenAI.

<a id="copyright-and-licenses"></a>
## Copyright and licenses

VirtualPaper's application code is distributed under the [GNU GPL version 3](../licenses/GPL-3.0.txt). Source code and release information are available from the [VirtualPaper repository](https://github.com/PaperHammer/VirtualPaper). The software's warranty and redistribution terms are stated in that license; mandatory legal rights remain unaffected.

Third-party components and model weights have their own copyright notices and licenses. See the [third-party information](../THIRD-PARTY.txt) and accompanying original license and NOTICE files. Translations and explanations are provided for reference; they do not replace the original terms or grant a blanket license to third-party materials.

Built-in wallpapers have separate provenance and terms. The video Santa Rosa Waves was created by Blervis and the Bigora Cascade footage by Termininja; the supplied adaptations use CC0. Other collection notices identify VirtualPaper's generated imagery or animation code. Keep each wallpaper's `NOTICE.txt` and any accompanying license when redistributing it. A publisher or adaptation credit does not replace the original creator's credit or change the original license.

CC0 does not require attribution; its creator/source records are retained for provenance and add no redistribution condition.
