using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public sealed class MainBgmPlaylist : MonoBehaviour
{
    [SerializeField] private List<AudioClip> tracks = new();
    [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
    [SerializeField] private bool playDuringPreparation = true;

    private readonly List<int> remainingTracks = new();
    private AudioSource source;
    private int lastTrack = -1;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = volume;
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "Main_Scene")
        {
            enabled = false;
            return;
        }
        PlayNextTrack();
    }

    private void Update()
    {
        source.volume = volume;
        DayNightCycle cycle = FindFirstObjectByType<DayNightCycle>();
        bool allowed = playDuringPreparation || cycle == null || !cycle.PreparingToOpen;
        if (!allowed)
        {
            if (source.isPlaying) source.Pause();
            return;
        }
        if (!source.isPlaying)
        {
            if (source.time > 0f && source.clip != null
                && source.time < source.clip.length - 0.1f)
                source.UnPause();
            else
                PlayNextTrack();
        }
    }

    private void PlayNextTrack()
    {
        tracks.RemoveAll(track => track == null);
        if (tracks.Count == 0) return;
        if (remainingTracks.Count == 0) RefillAndShuffle();

        int next = remainingTracks[0];
        remainingTracks.RemoveAt(0);
        lastTrack = next;
        source.clip = tracks[next];
        source.Play();
    }

    private void RefillAndShuffle()
    {
        remainingTracks.Clear();
        for (int i = 0; i < tracks.Count; i++) remainingTracks.Add(i);
        for (int i = remainingTracks.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (remainingTracks[i], remainingTracks[swap]) =
                (remainingTracks[swap], remainingTracks[i]);
        }
        if (remainingTracks.Count > 1 && remainingTracks[0] == lastTrack)
            (remainingTracks[0], remainingTracks[1]) =
                (remainingTracks[1], remainingTracks[0]);
    }
}
