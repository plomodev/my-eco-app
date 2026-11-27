using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace GameEcoBot;

/// <summary>
/// Suivi exclusif de la session audio Spotify.
/// Ignore systématiquement les navigateurs (Chrome, Edge, Firefox) et les autres applications,
/// pour afficher uniquement la musique en cours sur Spotify.
/// </summary>
public class AudioSessionTracker
{
    public string CurrentTitle { get; private set; } = "Aucune lecture";
    public string CurrentArtist { get; private set; } = "Spotify";
    public string CurrentSource { get; private set; } = "Spotify";
    public Image? CurrentThumbnail { get; private set; }
    public bool HasMedia { get; private set; } = false;
    public bool IsPlaying { get; private set; } = false;

    public event Action? OnTrackChanged;

    public async Task UpdateAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (manager != null)
            {
                // Recherche EXCLUSIVE de la session Spotify parmi toutes les sessions audio actives
                GlobalSystemMediaTransportControlsSession? spotifySession = null;
                var sessions = manager.GetSessions();
                if (sessions != null)
                {
                    foreach (var s in sessions)
                    {
                        string appId = s.SourceAppUserModelId ?? "";
                        if (appId.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                        {
                            spotifySession = s;
                            break;
                        }
                    }
                }

                // Vérification supplémentaire sur la session courante si elle provient de Spotify
                if (spotifySession == null)
                {
                    var cur = manager.GetCurrentSession();
                    if (cur != null && (cur.SourceAppUserModelId ?? "").Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                    {
                        spotifySession = cur;
                    }
                }

                if (spotifySession != null)
                {
                    var playbackInfo = spotifySession.GetPlaybackInfo();
                    IsPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                    var props = await spotifySession.TryGetMediaPropertiesAsync();
                    if (props != null && !string.IsNullOrWhiteSpace(props.Title))
                    {
                        string newTitle = props.Title.Length > 20 ? props.Title.Substring(0, 18) + "..." : props.Title;
                        string newArtist = string.IsNullOrWhiteSpace(props.Artist) ? "Spotify" : props.Artist;
                        if (newArtist.Length > 20) newArtist = newArtist.Substring(0, 18) + "...";

                        CurrentTitle = newTitle;
                        CurrentArtist = newArtist;
                        CurrentSource = "Spotify";
                        HasMedia = true;

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

            // Détection de secours directe via le titre de fenêtre du processus Spotify
            var procs = Process.GetProcessesByName("Spotify");
            foreach (var p in procs)
            {
                try
                {
                    string winTitle = p.MainWindowTitle;
                    if (!string.IsNullOrWhiteSpace(winTitle) &&
                        !winTitle.Equals("Spotify", StringComparison.OrdinalIgnoreCase) &&
                        !winTitle.StartsWith("Spotify ", StringComparison.OrdinalIgnoreCase))
                    {
                        int dash = winTitle.IndexOf(" - ");
                        if (dash > 0)
                        {
                            CurrentArtist = winTitle.Substring(0, dash).Trim();
                            CurrentTitle = winTitle.Substring(dash + 3).Trim();
                        }
                        else
                        {
                            CurrentTitle = winTitle.Trim();
                            CurrentArtist = "Spotify";
                        }
                        if (CurrentTitle.Length > 20) CurrentTitle = CurrentTitle.Substring(0, 18) + "...";
                        if (CurrentArtist.Length > 20) CurrentArtist = CurrentArtist.Substring(0, 18) + "...";

                        CurrentSource = "Spotify";
                        HasMedia = true;
                        IsPlaying = true;
                        OnTrackChanged?.Invoke();
                        return;
                    }
                }
                catch { }
            }
        }
        catch { }

        // Si Spotify ne joue aucun titre : afficher "Aucune lecture" (ne jamais afficher Chrome/Edge/Firefox)
        if (HasMedia || CurrentTitle != "Aucune lecture")
        {
            HasMedia = false;
            IsPlaying = false;
            CurrentTitle = "Aucune lecture";
            CurrentArtist = "Spotify";
            CurrentSource = "Spotify";
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
            if (manager != null)
            {
                foreach (var s in manager.GetSessions())
                {
                    if ((s.SourceAppUserModelId ?? "").Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                    {
                        await s.TryTogglePlayPauseAsync();
                        await UpdateAsync();
                        return;
                    }
                }
            }
        }
        catch { }
    }

    public async Task SkipNextAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (manager != null)
            {
                foreach (var s in manager.GetSessions())
                {
                    if ((s.SourceAppUserModelId ?? "").Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                    {
                        await s.TrySkipNextAsync();
                        await UpdateAsync();
                        return;
                    }
                }
            }
        }
        catch { }
    }

    public async Task SkipPreviousAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            if (manager != null)
            {
                foreach (var s in manager.GetSessions())
                {
                    if ((s.SourceAppUserModelId ?? "").Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                    {
                        await s.TrySkipPreviousAsync();
                        await UpdateAsync();
                        return;
                    }
                }
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
