using System;

namespace CatchTheTroublemaker
{
    // Serializable DTOs matching the JSON returned by server/index.js.
    // JsonUtility cannot deserialize a top-level array, so every response is an object.

    [Serializable]
    public class CTTLocalizedName
    {
        public string en;
        public string ca;
    }

    [Serializable]
    public class CTTMap
    {
        public int cols;
        public int rows;
        public string tiles;
    }

    [Serializable]
    public class CTTCoin
    {
        public int id;
        public float x;
        public float y;
    }

    [Serializable]
    public class CTTPlayer
    {
        public string id;
        public string name;
        public string role;
        public string team;
        public string family;
        public string color;
        public string shape;
        public float x;
        public float y;
        public int coins;
        public int lives;
        public bool eliminated;
        public float respawnIn;
        public bool grace;
        public float sprintIn;
        public bool sprinting;
        public bool isBot;
        public bool connected;
    }

    [Serializable]
    public class CTTStats
    {
        public int catches;
        public int coinsCollected;
        public int confiscated;
    }

    [Serializable]
    public class CTTStanding
    {
        public string id;
        public string name;
        public string role;
        public string team;
        public int coins;
        public int lives;
        public bool eliminated;
    }

    [Serializable]
    public class CTTResult
    {
        public string winnerTeam;
        public string winnerId;
        public string winnerName;
        public string reason;
        public long durationMs;
        public int goal;
        public CTTStats stats;
        public CTTStanding[] standings;
        public long finishedAt;
    }

    [Serializable]
    public class CTTLobbyPlayer
    {
        public string id;
        public string name;
        public string role;
        public bool isHost;
    }

    [Serializable]
    public class CTTRoleSlot
    {
        public string role;
        public string team;
        public string family;
        public string color;
        public CTTLocalizedName name;
        public string takenBy;
    }

    [Serializable]
    public class CTTLobby
    {
        public string code;
        public string status;
        public string hostId;
        public int seed;
        public CTTLobbyPlayer[] players;
        public CTTRoleSlot[] roles;
        /// <summary>Folder ids of the two chosen families (0-2 entries).</summary>
        public string[] families;
    }

    [Serializable]
    public class CTTEvent
    {
        public string type;
        public string id;
        public float x;
        public float y;
        public string winnerTeam;
        public string winnerId;
        public long now;
    }

    [Serializable]
    public class CTTState
    {
        public string status;
        public long now;
        public long tick;
        public long remainingMs;
        public int goal;
        public CTTCoin[] coins;
        public CTTPlayer[] players;
        public CTTEvent[] events;
        public CTTResult result;
        public CTTMap map;
        public CTTLobby lobby;
    }

    [Serializable]
    public class CTTRoomResponse
    {
        public string code;
        public string playerId;
        public CTTLobby lobby;
        public string error;
    }

    [Serializable]
    public class CTTLobbyResponse
    {
        public bool ok;
        public CTTLobby lobby;
        public string error;
    }

    [Serializable]
    public class CTTOkResponse
    {
        public bool ok;
        public string error;
    }

    [Serializable]
    public class CTTResultResponse
    {
        public string status;
        public bool finished;
        public CTTResult result;
        public string error;
    }

    // --- Requests ---
    [Serializable]
    public class CTTNameRequest
    {
        public string name;
    }

    [Serializable]
    public class CTTJoinRequest
    {
        public string name;
        public string playerId;
    }

    [Serializable]
    public class CTTRoleRequest
    {
        public string playerId;
        public string role;
    }

    [Serializable]
    public class CTTFamiliesRequest
    {
        public string playerId;
        public string[] families;
    }

    [Serializable]
    public class CTTStartRequest
    {
        public string playerId;
    }

    [Serializable]
    public class CTTInputRequest
    {
        public string playerId;
        public int dx;
        public int dy;
        public bool sprint;
    }
}
