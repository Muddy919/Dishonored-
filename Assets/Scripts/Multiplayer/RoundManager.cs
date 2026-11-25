using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Put on an empty GameObject in the Battle scene (with a NetworkObject, same as
/// BattleSpawner - it'll spawn automatically as part of the scene load).
/// </summary>
public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Match Rules")]
    [SerializeField] private int roundsToWin = 3;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private float matchDurationSeconds = 180f; // 3 minutes
    [SerializeField] private float roundPopupDuration = 2.5f;
    [SerializeField] private float matchPopupDuration = 3f;

    [Header("Spawn Points")]
    [SerializeField] private Transform hostSpawnPoint;
    [SerializeField] private Transform clientSpawnPoint;

    [Header("Scene")]
    [Tooltip("Exact name of the character-select scene, must match Build Settings.")]
    [SerializeField] private string selectSceneName = "CharacterSelect";

    [Header("UI - Timer & Score")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text scoreText; // shows e.g. "2 - 4"

    [Header("UI - Round Result Popup")]
    [SerializeField] private GameObject roundPopupPanel;
    [SerializeField] private TMP_Text roundPopupText;

    [Header("UI - Match Result Popup")]
    [SerializeField] private GameObject matchPopupPanel;
    [SerializeField] private TMP_Text matchPopupText;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip killSound;

    private readonly NetworkVariable<float> timeRemaining = new NetworkVariable<float>(180f);
    private bool matchEnded;
    private bool roundLocked;

    public bool IsRoundLocked => roundLocked || matchEnded;

    private void Awake() => Instance = this;

    public override void OnNetworkSpawn()
    {
        matchEnded = false;
        roundLocked = false;

        if (IsServer) timeRemaining.Value = matchDurationSeconds;

        timeRemaining.OnValueChanged += (_, _) => UpdateTimerUI();
        UpdateTimerUI();

        if (MatchState.Instance != null)
        {
            MatchState.Instance.HostScore.OnValueChanged += (_, _) => UpdateScoreUI();
            MatchState.Instance.ClientScore.OnValueChanged += (_, _) => UpdateScoreUI();
        }
        UpdateScoreUI();

        if (roundPopupPanel != null) roundPopupPanel.SetActive(false);
        if (matchPopupPanel != null) matchPopupPanel.SetActive(false);
    }

    private void Update()
    {
        if (!IsServer || matchEnded) return;

        timeRemaining.Value -= Time.deltaTime;

        if (timeRemaining.Value <= 0f)
        {
            timeRemaining.Value = 0f;
            EndMatchByTimeoutServer();
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        int seconds = Mathf.Max(0, Mathf.CeilToInt(timeRemaining.Value));
        timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private void UpdateScoreUI()
    {
        if (scoreText == null || MatchState.Instance == null) return;
        scoreText.text = $"{MatchState.Instance.HostScore.Value} - {MatchState.Instance.ClientScore.Value}";
    }

    // Called by PlayerHealth on the server when a fighter's health hits 0.
    public void OnFighterDefeatedServer(ulong loserClientId)
    {
        if (!IsServer || matchEnded || roundLocked) return;

        // Lock immediately so a second kill during the popup can't double-score.
        roundLocked = true;

        bool hostWonRound = loserClientId != NetworkManager.ServerClientId;

        if (hostWonRound)
            MatchState.Instance.HostScore.Value++;
        else
            MatchState.Instance.ClientScore.Value++;

        PlayRoundResultClientRpc(hostWonRound);

        int hostScore = MatchState.Instance.HostScore.Value;
        int clientScore = MatchState.Instance.ClientScore.Value;

        if (hostScore >= roundsToWin || clientScore >= roundsToWin)
            StartCoroutine(EndMatchSequence(true, hostScore >= roundsToWin));
        else
            StartCoroutine(RoundTransitionSequence());
    }

    private void EndMatchByTimeoutServer()
    {
        if (matchEnded) return;

        int hostScore = MatchState.Instance.HostScore.Value;
        int clientScore = MatchState.Instance.ClientScore.Value;

        if (hostScore == clientScore)
            StartCoroutine(EndMatchSequence(false, false)); // draw
        else
            StartCoroutine(EndMatchSequence(true, hostScore > clientScore));
    }

    private IEnumerator RoundTransitionSequence()
    {
        yield return new WaitForSeconds(roundPopupDuration);
        HideRoundPopupClientRpc();
        yield return new WaitForSeconds(respawnDelay);
        RespawnBothServer();
        roundLocked = false;
    }

    private IEnumerator EndMatchSequence(bool hasWinner, bool hostWon)
    {
        matchEnded = true;
        roundLocked = true;
        yield return new WaitForSeconds(roundPopupDuration);
        AnnounceMatchWinnerClientRpc(hasWinner, hostWon);
        yield return new WaitForSeconds(matchPopupDuration);

        // Clear fighters before returning to select so rematches don't stack bodies.
        PlayerHealth.DespawnAllFighters();
        yield return null;

        NetworkManager.Singleton.SceneManager.LoadScene(selectSceneName, LoadSceneMode.Single);
    }

    private void RespawnBothServer()
    {
        foreach (var kvp in NetworkManager.ConnectedClients)
        {
            var playerObj = kvp.Value.PlayerObject;
            if (playerObj == null) continue;

            var spawnPoint = kvp.Key == NetworkManager.ServerClientId ? hostSpawnPoint : clientSpawnPoint;

            var health = playerObj.GetComponent<PlayerHealth>();
            if (health != null)
                health.RespawnServer(spawnPoint.position, spawnPoint.rotation);
        }
    }

    [ClientRpc]
    private void PlayRoundResultClientRpc(bool hostWonRound)
    {
        if (audioSource != null && killSound != null)
            audioSource.PlayOneShot(killSound);

        if (roundPopupPanel != null) roundPopupPanel.SetActive(true);
        if (roundPopupText != null)
            roundPopupText.text = hostWonRound ? "Host wins the round!" : "Client wins the round!";
    }

    [ClientRpc]
    private void HideRoundPopupClientRpc()
    {
        if (roundPopupPanel != null) roundPopupPanel.SetActive(false);
    }

    [ClientRpc]
    private void AnnounceMatchWinnerClientRpc(bool hasWinner, bool hostWon)
    {
        if (roundPopupPanel != null) roundPopupPanel.SetActive(false);
        if (matchPopupPanel != null) matchPopupPanel.SetActive(true);
        if (matchPopupText != null)
            matchPopupText.text = !hasWinner ? "It's a draw!" : (hostWon ? "Host is the winner!" : "Client is the winner!");
    }
}
