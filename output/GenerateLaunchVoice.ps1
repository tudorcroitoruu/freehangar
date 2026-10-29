Add-Type -AssemblyName System.Speech
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
$speaker.Rate = -1
$lines = @{ Ready = 'Get ready for launch.'; Three = 'Three.'; Two = 'Two.'; One = 'One.'; Protest = 'No! No! Let me out!' }
foreach ($item in $lines.GetEnumerator()) {
    $speaker.SetOutputToWaveFile(('F:\Projects\freehangar\Assets\LaunchRoom\Audio\' + $item.Key + '.wav'))
    $speaker.Speak($item.Value)
}
$speaker.Dispose()
