using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace GameEcoBot;

public class AudioSessionTracker
{
    public string CurrentTitle { get; private set; } = "Aucun son";
    public string CurrentArtist { get; private set; } = "En attente...";
    public string CurrentSource { get; private set; } = "via Média";
    public Image? CurrentThumbnail { get; private set; }
    public bool HasMedia { get; private set; } = false;
    public bool IsPlaying { get; private set; } = false;

    public event Action? OnTrackChanged;

    public async Task UpdateAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager?.GetCurrentSession();
            if (session != null)
            {
                var playbackInfo = session.GetPlaybackInfo();
                IsPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                var props = await session.TryGetMediaPropertiesAsync();
                if (props != null && !string.IsNullOrWhiteSpace(props.Title))
                {
                    string newTitle = props.Title.Length > 20 ? props.Title.Substring(0, 18) + "..." : props.Title;
                    string newArtist = string.IsNullOrWhiteSpace(props.Artist) ? "Média" : props.Artist;
                    if (newArtist.Length > 20) newArtist = newArtist.Substring(0, 18) + "...";

                    bool changed = newTitle != CurrentTitle || newArtist != CurrentArtist || !HasMedia;

                    CurrentTitle = newTitle;
                    CurrentArtist = newArtist;
                    HasMedia = true;

                    // Détecter la source (Spotify, Chrome, etc.)
                    try
                    {
                        string appId = session.SourceAppUserModelId ?? "";
                        if (appId.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                            CurrentSource = "via Spotify";
                        else if (appId.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
                            CurrentSource = "via Chrome";
                        else if (appId.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
                            CurrentSource = "via Firefox";
                        else if (appId.Contains("Edge", StringComparison.OrdinalIgnoreCase))
                            CurrentSource = "via Edge";
                        else if (appId.Contains("Groove", StringComparison.OrdinalIgnoreCase) || appId.Contains("ZuneMusic", StringComparison.OrdinalIgnoreCase))
                            CurrentSource = "via Musique";
                        else if (!string.IsNullOrWhiteSpace(appId))
                        {
                            var parts = appId.Split(new char[] { '\\', '/', '!' }, StringSplitOptions.RemoveEmptyEntries);
                            string lastName = parts[^1];
                            if (lastName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                lastName = lastName[..^4];
                            CurrentSource = $"via {lastName}";
                        }
                        else
                            CurrentSource = "via Média";
                    }
                    catch { CurrentSource = "via Média"; }

                    if (props.Thumbnail != null)
                    {
                        try
                        {
                            using var stream = await props.Thumbnail.OpenReadAsync();
                            using var netStream = stream.AsStreamForRead();
                            using var rawImg = Image.FromStream(netStream);
                            var oldImg = CurrentThumbnail;
                            CurrentThumbnail = CreateRoundedImage(rawImg, 42, 42, 8);
                            oldImg?.Dispose();
                        }
                        catch
                        {
                            var oldImg = CurrentThumbnail;
                            CurrentThumbnail = null;
                            oldImg?.Dispose();
                        }
                    }
                    else
                    {
                        var oldImg = CurrentThumbnail;
                        CurrentThumbnail = null;
                        oldImg?.Dispose();
                    }

                    OnTrackChanged?.Invoke();
                    return;
                }
            }
        }
        catch { }

        if (HasMedia || CurrentTitle != "Aucun son")
        {
            HasMedia = false;
            IsPlaying = false;
            CurrentTitle = "Aucun son";
            CurrentArtist = "En attente de Spotify/Média";
            CurrentSource = "via Média";
            var oldImg = CurrentThumbnail;
            CurrentThumbnail = null;
            oldImg?.Dispose();
            OnTrackChanged?.Invoke();
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager?.GetCurrentSession();
            if (session != null)
            {
                await session.TryTogglePlayPauseAsync();
                await UpdateAsync();
            }
        }
        catch { }
    }

    public async Task SkipNextAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager?.GetCurrentSession();
            if (session != null)
            {
                await session.TrySkipNextAsync();
                await UpdateAsync();
            }
        }
        catch { }
    }

    public async Task SkipPreviousAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager?.GetCurrentSession();
            if (session != null)
            {
                await session.TrySkipPreviousAsync();
                await UpdateAsync();
            }
        }
        catch { }
    }

    private Image CreateRoundedImage(Image src, int width, int height, int cornerRadius)
    {
        Bitmap bmp = new Bitmap(width, height);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using GraphicsPath path = new GraphicsPath();
        path.AddArc(0, 0, cornerRadius * 2, cornerRadius * 2, 180, 90);
        path.AddArc(width - cornerRadius * 2, 0, cornerRadius * 2, cornerRadius * 2, 270, 90);
        path.AddArc(width - cornerRadius * 2, height - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 0, 90);
        path.AddArc(0, height - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 90, 90);
        path.CloseFigure();

        g.SetClip(path);
        g.DrawImage(src, 0, 0, width, height);
        return bmp;
    }
}
