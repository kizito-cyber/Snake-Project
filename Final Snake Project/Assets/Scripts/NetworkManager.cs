using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using UnityEngine;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject LoadingPanel;
    [SerializeField] private byte MaxPlayersPerRoom = 15;

    // which room?suffix we’re currently targeting
    private int _roomIndex = 1;

    private string CurrentRoomName => $"GulperRoom_{_roomIndex}";

    void Start()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogError("Not connected to Photon.");
            return;
        }

        if (PhotonNetwork.InLobby)
            TryJoinOrCreate();
        else
            PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined lobby, now joining/creating room...");
        TryJoinOrCreate();
    }

    private void TryJoinOrCreate()
    {
        Debug.Log($"Attempting to join room {CurrentRoomName}");
        PhotonNetwork.JoinRoom(CurrentRoomName);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log($"JoinRoom {CurrentRoomName} failed ({returnCode}): {message}");
        if (returnCode == ErrorCode.GameDoesNotExist)
        {
            // no such room yet ? create it
            Debug.Log($"Creating room {CurrentRoomName}");
            var opts = new RoomOptions { MaxPlayers = MaxPlayersPerRoom };
            PhotonNetwork.CreateRoom(CurrentRoomName, opts, TypedLobby.Default);
        }
        else if (returnCode == ErrorCode.GameFull)
        {
            // room exists but is full ? bump index and retry
            _roomIndex++;
            TryJoinOrCreate();
        }
        else
        {
            Debug.LogError($"Unexpected OnJoinRoomFailed code={returnCode}");
        }
    }

    public override void OnCreatedRoom()
    {
        Debug.Log($"Created room {CurrentRoomName}");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined room {PhotonNetwork.CurrentRoom.Name} ({PhotonNetwork.CurrentRoom.PlayerCount}/{MaxPlayersPerRoom})");
        StartCoroutine(WaitOnJoin());
    }

    IEnumerator WaitOnJoin()
    {
        yield return new WaitForSeconds(1.5f);
        LoadingPanel.SetActive(false);
        // now call your spawn/init logic
    }
}
