using System.Reflection;

using System.Runtime.CompilerServices;

using System.Runtime.InteropServices;



[assembly: InternalsVisibleTo("RenderReadmeGraph")]



[assembly: Guid("b8d4f02a-5c3e-499b-0f2d-7e6b9c1d4e38")]



[assembly: AssemblyVersion("0.1.0.0")]

[assembly: AssemblyFileVersion("0.1.0.0")]



[assembly: AssemblyTitle("AutoFocusGraphs")]

[assembly: AssemblyDescription("Watches N.I.N.A. AutoFocus reports and posts V-curve graphs to Discord, Telegram, Slack, and email.")]



[assembly: AssemblyCompany("Chris Flory @starjunkie")]

[assembly: AssemblyProduct("AutoFocusGraphs")]

[assembly: AssemblyCopyright("Copyright © 2026 Chris Flory")]



[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.3.0.1047")]



[assembly: AssemblyMetadata("License", "MIT")]

[assembly: AssemblyMetadata("LicenseURL", "https://opensource.org/licenses/MIT")]

[assembly: AssemblyMetadata("Repository", "https://github.com/chrisflory/AutoFocusGraphs")]



[assembly: AssemblyMetadata("Homepage", "https://github.com/chrisflory/AutoFocusGraphs")]

[assembly: AssemblyMetadata("Tags", "autofocus,graph,discord,telegram,slack,email,webhook,notification")]

[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/chrisflory/AutoFocusGraphs/blob/develop/CHANGELOG.md")]



[assembly: AssemblyMetadata("FeaturedImageURL", "https://raw.githubusercontent.com/chrisflory/AutoFocusGraphs/develop/assets/webhook-icon-af-graphs.png")]

[assembly: AssemblyMetadata("ScreenshotURL", "")]

[assembly: AssemblyMetadata("AltScreenshotURL", "")]

[assembly: AssemblyMetadata("LongDescription", @"## How it works



![AutoFocusGraphs pipeline](https://raw.githubusercontent.com/chrisflory/AutoFocusGraphs/develop/assets/flowchart.png)



Per-run V-curve posts, sequence digests, and session digests to **Discord**, **Telegram**, **Slack**, and **email**.



## Setup

1. Enable a destination under Options → Plugins → AutoFocusGraphs

2. Discord: channel webhook URL → Test webhook

3. Telegram: bot token from @BotFather + chat ID → Test Telegram

4. Slack: bot token + channel ID → Test Slack

5. Email: SMTP settings → Test email



Each destination can be enabled independently. Graph rendering is shared across all channels.")]



[assembly: ComVisible(false)]

[assembly: AssemblyConfiguration("")]

[assembly: AssemblyTrademark("")]

[assembly: AssemblyCulture("")]

