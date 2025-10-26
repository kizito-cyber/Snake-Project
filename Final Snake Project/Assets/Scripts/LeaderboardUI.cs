using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using Photon.Pun;

public class LeaderboardUI : MonoBehaviour
{
    [Tooltip("Assign your TextMeshProUGUI slots here in rank order")]
    [SerializeField] private TextMeshProUGUI[] slots;

    // Remember guest codes so they stay consistent this session
    private Dictionary<string, string> _guestCodes = new Dictionary<string, string>();

    void OnEnable()
    {
        Refresh();
    }

    /// <summary>
    /// Gathers all SnakeController instances, sorts them by score, 
    /// then fills the slots with rank, displayName, and score.
    /// </summary>
    public void Refresh()
    {
        // 1) find all snakes
        var snakes = FindObjectsOfType<SnakeController>()
            .Where(s => s.photonView != null && s.photonView.Owner != null)
            .ToList();

        // 2) build (displayName, score, isLocal) list
        var entries = new List<(string name, int score, bool isLocal)>();
        foreach (var s in snakes)
        {
            string nick = s.photonView.Owner.NickName;
            string display = !string.IsNullOrEmpty(nick)
                ? nick
                : FormatGuestCode(s.photonView.Owner.UserId);

            bool isLocal = s.photonView.IsMine;
            entries.Add((display, s.currentScore, isLocal));
        }

        // 3) sort descending by score
        var sorted = entries
            .OrderByDescending(e => e.score)
            .ToList();

        // 4) take top 5
        var top5 = sorted.Take(5).ToList();

        // 5) fill first 5 slots
        for (int i = 0; i < 5; i++)
        {
            if (i < top5.Count)
                slots[i].text = $"{i + 1}. {top5[i].name} — {top5[i].score}";
            else
                slots[i].text = $"{i + 1}. ---";
        }

        // 6) see if local player is in top5
        bool localInTop5 = top5.Any(e => e.isLocal);

        // 7) if not, find local player’s overall rank and display in slot[5] (if exists)
        if (!localInTop5 && slots.Length > 5)
        {
            int localIndex = sorted.FindIndex(e => e.isLocal);
            if (localIndex >= 0)
            {
                var you = sorted[localIndex];
                int yourRank = localIndex + 1;
                slots[5].text = $"{yourRank}. {you.name} — {you.score}";
            }
            else
            {
                // No local entry (shouldn't happen)
                slots[5].text = $"6. ---";
            }
        }
        else if (slots.Length > 5)
        {
            // clear the 6th slot if local is already in top5
            slots[5].text = "";
        }

        // 8) clear any further slots beyond the ones we used
        for (int i = 6; i < slots.Length; i++)
            slots[i].text = "";
    }


    /// <summary>
    /// Given a Photon UserId, returns a cached Guest?XXXXX code.
    /// </summary>
    private string FormatGuestCode(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            userId = "anon";

        if (!_guestCodes.TryGetValue(userId, out var code))
        {
            code = GenerateGuestSuffix();
            _guestCodes[userId] = code;
        }
        return $"Guest-{code}";
    }

    /// <summary>
    /// Generates a random 5?letter uppercase code (A–Z).
    /// </summary>
    private string GenerateGuestSuffix()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var sb = new StringBuilder(5);
        var rng = new System.Random();
        for (int i = 0; i < 5; i++)
            sb.Append(chars[rng.Next(chars.Length)]);
        return sb.ToString();
    }
}
