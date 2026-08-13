using UnityEngine;

/// <summary>
/// Removes gameplay behaviour from the copied map used behind the home menu.
/// Ambient NPC and car spawners, NavMesh movement, animation, appearance and
/// automatic doors are intentionally left enabled.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class HomeMenuAmbientMode : MonoBehaviour
{
    private void Awake()
    {
        DisableExtraAudioListeners();
        DisableAndHidePlayers();

        DisableAll<DayNightCycle>();
        DisableAll<OpeningSequence>();
        DisableAll<DeveloperConsole>();
        DisableAll<PlayerInteraction>();
        DisableAll<CashierInteractable>();
        DisableAll<WorkstationInteractable>();
        DisableAll<CheckoutStation>();
        DisableAll<ShelfStation>();
        DisableAll<FridgeDoor>();
        DisableAll<OpenFridge>();
        DisableAll<IceCreamMachine>();
        DisableAll<CookingStation>();
        DisableAll<ChairStation>();
        DisableAll<MainBgmPlaylist>();
    }

    private static void DisableExtraAudioListeners()
    {
        AudioListener keep = Camera.main != null
            ? Camera.main.GetComponent<AudioListener>()
            : null;
        AudioListener[] listeners = FindObjectsByType<AudioListener>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (AudioListener listener in listeners)
        {
            if (listener != null && listener != keep)
                listener.enabled = false;
        }
    }

    private static void DisableAndHidePlayers()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (PlayerController player in players)
        {
            if (player != null)
                player.gameObject.SetActive(false);
        }
    }

    private static void DisableAll<T>() where T : Behaviour
    {
        T[] behaviours = FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (T behaviour in behaviours)
        {
            if (behaviour != null)
                behaviour.enabled = false;
        }
    }
}
