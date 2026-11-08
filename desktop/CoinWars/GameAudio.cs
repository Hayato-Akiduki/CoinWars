using System.IO;
using System.Media;
using System.Text;
namespace CoinWars;

/// <summary>Short locally synthesized PCM cues. No audio downloads or playback processes.</summary>
public static class GameAudio
{
    static readonly Dictionary<string,(SoundPlayer player,MemoryStream stream)> sounds=[];
    public static void Stop() { foreach(var sound in sounds.Values) { try { sound.player.Stop(); } catch { } } }
    public static void Play(string name)
    {
        try
        {
            if(!sounds.TryGetValue(name,out var sound))
            {
                double[] notes=name switch {
                    "buy"=>[660,990], "merge"=>[523,659,784,1047], "win"=>[523,659,784,1047],
                    "lose"=>[330,277,220], "start"=>[330,440,660], "skill"=>[440,880],
                    "charge"=>[220,330,440,660], "shuffle"=>[520,620], "hit"=>[170], _=>[720]};
                int noteSamples=name=="hit"?900:2000;
                int count=notes.Length*noteSamples;
                var stream=new MemoryStream();
                using(var writer=new BinaryWriter(stream,Encoding.ASCII,true))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);
                    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
                    writer.Write((short)1);writer.Write((short)1);writer.Write(22050);writer.Write(44100);
                    writer.Write((short)2);writer.Write((short)16);
                    writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                    for(int n=0;n<count;n++)
                    {
                        double t=(n%noteSamples)/22050.0;
                        double envelope=Math.Sin(Math.PI*(n%noteSamples)/noteSamples);
                        writer.Write((short)(Math.Sin(2*Math.PI*notes[n/noteSamples]*t)*envelope*1700));
                    }
                }
                stream.Position=0;var player=new SoundPlayer(stream);player.Load();sound=(player,stream);sounds[name]=sound;
            }
            sound.player.Play();
        }
        catch { /* Audio devices are optional; play remains available without one. */ }
    }
}
