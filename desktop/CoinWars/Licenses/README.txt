CoinWars third-party software notices
Reviewed: 2026-11-09

These documents cover bundled third-party components, not a license grant for
CoinWars source code or character artwork.

.NET runtime and Windows Desktop runtime: 10.0.14, win-x64.
LICENSE files and runtime notices were copied without modification from the
Microsoft.NETCore.App.Runtime.win-x64 and
Microsoft.WindowsDesktop.App.Runtime.win-x64 NuGet packages used to publish.
Both packages declare the MIT license in their .nuspec metadata.

NET-Runtime-LICENSE.txt
NET-Runtime-THIRD-PARTY-NOTICES.txt
WindowsDesktop-LICENSE.txt

The WindowsDesktop package contains no separate third-party notices file.
The WPF and Windows Forms notices were retrieved without modification from
the exact source revision recorded in its package metadata:
https://github.com/dotnet/dotnet/tree/0427b9abbead3fa64c6655eb4290c537397131fa
src/wpf/THIRD-PARTY-NOTICES.TXT -> WPF-THIRD-PARTY-NOTICES.txt
src/winforms/THIRD-PARTY-NOTICES.TXT -> WindowsForms-THIRD-PARTY-NOTICES.txt

Installer: Inno Setup 6.7.4 by Jordan Russell and Martijn Laan.
InnoSetup-LICENSE.txt was copied from the installed compiler distribution.
https://jrsoftware.org/
The installer engine's copyright notices have not been removed or modified.

Yu Gothic UI is requested from the user's Windows installation; font files
are not bundled. Sound effects are synthesized by CoinWars code.

When updating runtime packs, review their license and third-party notices,
replace these documents, and update reviewed-packs.json together.
