from pathlib import Path

path = Path(__file__).resolve().parents[1] / "Options.xaml"
text = path.read_text(encoding="utf-8")
start = text.index('<StackPanel Orientation="Vertical"')
end = text.rindex("</StackPanel>")
inner = text[start : end + len("</StackPanel>")]


def section_between(content, start_marker, end_marker):
    s = content.index(start_marker)
    e = content.index(end_marker, s + len(start_marker))
    return content[s:e]


general = section_between(
    inner,
    '<Grid Margin="0,0,0,10" ToolTip="Master switch',
    '<TextBlock Text="Discord"',
)
discord = section_between(inner, '<TextBlock Text="Discord"', '<TextBlock Text="Telegram"')
telegram = section_between(inner, '<TextBlock Text="Telegram"', '<TextBlock Text="Graph"')
graph = section_between(inner, '<TextBlock Text="Graph"', '<TextBlock Text="Quality gate"')
quality = section_between(inner, '<TextBlock Text="Quality gate"', "</StackPanel>")

role_start = '<TextBlock Text="Discord alert role ID'
role_end = '<Grid Margin="0,0,0,10" ToolTip="Hooks NINA autofocus lifecycle'
role_block = section_between(quality, role_start, role_end)
quality = quality.replace(role_block, "")

discord = discord.replace(
    '<TextBlock Text="Discord" FontSize="16" FontWeight="Bold" Margin="0,12,0,10" />\n\n            ',
    "",
)
graph = graph.replace(
    '<TextBlock Text="Graph" FontSize="16" FontWeight="Bold" Margin="0,12,0,10" />\n\n            ',
    "",
)
telegram = telegram.replace(
    '<TextBlock Text="Telegram" FontSize="16" FontWeight="Bold" Margin="0,12,0,10" />\n\n            ',
    "",
)
quality = quality.replace(
    '<TextBlock Text="Quality gate" FontSize="16" FontWeight="Bold" Margin="0,12,0,10" />\n\n            ',
    "",
)

slack = """
            <Grid Margin="0,0,0,10" ToolTip="Post autofocus graphs and digests to a Slack channel.">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <Grid Grid.Column="0">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto" SharedSizeGroup="ToggleLabel" />
                        <ColumnDefinition Width="*" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Text="Enable Slack" VerticalAlignment="Center" TextWrapping="Wrap" MaxWidth="420" />
                    <Line Grid.Column="1" Stretch="Fill" X1="0" Y1="0" X2="1" Y2="0" Stroke="#666666" StrokeThickness="1" StrokeDashArray="1 3" VerticalAlignment="Center" Margin="6,0" />
                </Grid>
                <CheckBox Grid.Column="1" IsChecked="{Binding SlackEnabled}" VerticalAlignment="Center" />
            </Grid>

            <TextBlock Text="Slack bot token" Margin="0,0,0,4" />
            <TextBox Text="{Binding SlackBotToken, UpdateSourceTrigger=PropertyChanged}" HorizontalAlignment="Stretch" MaxWidth="640" Margin="0,0,0,4" />
            <TextBlock Text="Bot User OAuth Token (xoxb-...) from your Slack app. Needs chat:write and files:write." Margin="0,0,0,8" Opacity="0.7" TextWrapping="Wrap" />

            <TextBlock Text="Slack channel ID" Margin="0,0,0,4" />
            <TextBox Text="{Binding SlackChannelId, UpdateSourceTrigger=PropertyChanged}" MinWidth="240" Margin="0,0,0,4" />
            <TextBlock Text="Channel ID (C... or G...). Invite the bot to the channel before testing." Margin="0,0,0,8" Opacity="0.7" TextWrapping="Wrap" />

            <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
                <Button Content="Test Slack" Command="{Binding TestSlackCommand}" HorizontalAlignment="Left" Padding="12,4"
                        ToolTip="Send a short test message via the Slack Web API." />
                <TextBlock Text="✓" FontSize="20" FontWeight="Bold" Foreground="#2ECC71" VerticalAlignment="Center" Margin="10,0,0,0"
                           Visibility="{Binding SlackTestSuccessVisible}" ToolTip="Test message posted to Slack." />
                <TextBlock Text="✗" FontSize="20" FontWeight="Bold" Foreground="#E74C3C" VerticalAlignment="Center" Margin="10,0,0,0"
                           Visibility="{Binding SlackTestFailureVisible}" ToolTip="{Binding SlackTestToolTip}" />
            </StackPanel>
"""


def tab(name, body):
    return f"""            <TabItem Header="{name}">
                    <StackPanel Margin="0,4,0,0" Grid.IsSharedSizeScope="True">
{body.strip()}
                    </StackPanel>
            </TabItem>"""


out = f"""<ResourceDictionary
    x:Class="AutoFocusGraphs.Options"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <DataTemplate x:Key="AutoFocusGraphs_Options">
        <TabControl Loaded="OptionsPanel_Loaded" Grid.IsSharedSizeScope="True" VerticalAlignment="Top">
{tab("General", general)}
{tab("Discord", discord + "\n\n            " + role_block)}
{tab("Graph", graph)}
{tab("Telegram", telegram)}
{tab("Slack", slack)}
{tab("Quality &amp; posting", quality)}
        </TabControl>
    </DataTemplate>
</ResourceDictionary>
"""
path.write_text(out, encoding="utf-8")
print(f"Wrote {path}")
