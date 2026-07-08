"""Rebuild Options.xaml: single Graph tab (graph + quality/posting), destination tabs unchanged."""
from pathlib import Path
import re

path = Path(__file__).resolve().parents[1] / "Options.xaml"
text = path.read_text(encoding="utf-8")

tab_pattern = re.compile(
    r'\s*<TabItem Header="(?P<header>[^"]+)">\s*'
    r'<StackPanel Margin="0,4,0,0" Grid.IsSharedSizeScope="True">\s*'
    r'(?P<body>.*?)\s*'
    r'</StackPanel>\s*'
    r'</TabItem>',
    re.DOTALL,
)
tabs = {m.group("header"): m.group("body").strip() for m in tab_pattern.finditer(text)}

required = ["Graph", "Discord", "Telegram", "Slack", "Quality &amp; posting"]
missing = [h for h in required if h not in tabs]
if missing:
    raise SystemExit(f"Missing tabs: {missing}")

quality_header = (
    '<TextBlock Text="Quality &amp; posting" FontSize="16" FontWeight="Bold" '
    'Margin="0,16,0,10" />'
)
graph_body = tabs["Graph"] + "\n\n            " + quality_header + "\n\n            " + tabs["Quality &amp; posting"]

order = [
    ("Graph", graph_body),
    ("Discord", tabs["Discord"]),
    ("Telegram", tabs["Telegram"]),
    ("Slack", tabs["Slack"]),
]


def tab(name, body):
    return f"""            <TabItem Header="{name}">
                    <StackPanel Margin="0,4,0,0" Grid.IsSharedSizeScope="True">
{body}
                    </StackPanel>
            </TabItem>"""


out = f"""<ResourceDictionary
    x:Class="AutoFocusGraphs.Options"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <DataTemplate x:Key="AutoFocusGraphs_Options">
        <TabControl Loaded="OptionsPanel_Loaded" Grid.IsSharedSizeScope="True" VerticalAlignment="Top">
{chr(10).join(tab(name, body) for name, body in order)}
        </TabControl>
    </DataTemplate>
</ResourceDictionary>
"""
path.write_text(out, encoding="utf-8")
print(f"Merged Quality & posting into Graph; wrote {path}")
