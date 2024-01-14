using System.Collections.Generic;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UI;

public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public class ChessBoard : MonoBehaviour
{
    [Header("Art Stuff")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.0f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSize = 1.0f;
    [SerializeField] private float deathSpacing = 0.69f;
    [SerializeField] private float setX = 1.0f;
    [SerializeField] private float setY = -0.8f;
    [SerializeField] private float dragOffset = 0.8f;
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private GameObject rematchIndicator;
    [SerializeField] private Button rematchButton;

    [Header("Prefabs ++")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    // LOGIC 
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private ChessPiece[,] chessPieces;
    private ChessPiece currentlyDragging;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<ChessPiece> deadWhite = new List<ChessPiece>();
    private List<ChessPiece> deadBlack = new List<ChessPiece>();
    private GameObject[,] tiles;
    private Vector2Int currentHover;
    private Vector3 bounds;
    private int isWhiteTurn;
    private SpecialMove specialMove;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();
    private Camera currentCamera;

    //Multi logic
    private int playerCount = -1;
    private int currentTeam = -1;
    private bool localGame = true;
    private bool[] playerRematch = new bool[2];

    private void Start()
    {
        isWhiteTurn = 0;

        GenerateAllTiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        SpawnAllPieces();
        PositionAllPieces();

        RegisterEvents();
    }
    private void Update()
    {
        if(!currentCamera)
        {
            currentCamera = Camera.main;
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile","Hover","Highlight")))
        {
            //informatiile despre tile-ul unde este pus mouse-ul
            Vector2Int hitPosition = TileIndex(info.transform.gameObject);

            //cand nu am mouse-ul peste un tile
            if(currentHover == -Vector2Int.one)
            {
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            // daca am deja mouse-ul pe un tile , schimba-l tile-ul
            if(currentHover != hitPosition)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMoves(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            //cand apas cu mouse-ul (click stanga)
            if (Input.GetMouseButtonDown(0))
            {
                if(chessPieces[hitPosition.x,hitPosition.y] != null)
                {
                    //tura mea?
                    if((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn == 0 && currentTeam == 0) || (chessPieces[hitPosition.x, hitPosition.y].team == 1 && isWhiteTurn == 1 && currentTeam == 1))
                    {
                        currentlyDragging = chessPieces[hitPosition.x, hitPosition.y];

                        //Lista de unde poate merge piesa + highlight tiles
                        availableMoves = currentlyDragging.GetAvailableMoves(ref chessPieces,TILE_COUNT_X,TILE_COUNT_Y);
                        //Lista de mutari speciale
                        specialMove = currentlyDragging.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

                        PreventCheck();
                        HighlightTiles();
                    }
                }
            }

            //cand dau drumul la mouse (click stanga)
            if (currentlyDragging != null && Input.GetMouseButtonUp(0))
            {
                Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);

                if (ContainsValidMoves(ref availableMoves, new Vector2Int(hitPosition.x, hitPosition.y)))
                {
                    MoveTo(previousPosition.x, previousPosition.y, hitPosition.x, hitPosition.y);

                    //Net implemention
                    NetMakeMove nm = new NetMakeMove();
                    nm.originalX = previousPosition.x;
                    nm.originalY = previousPosition.y;
                    nm.destinationX = hitPosition.x;
                    nm.destinationY = hitPosition.y;
                    nm.teamId = currentTeam;
                    Client.Instance.SendToServer(nm);
                }
                else
                {
                    currentlyDragging.SetPotisition(GetTileCenter(previousPosition.x, previousPosition.y));
                    currentlyDragging = null;
                    RemoveHiglightTiles();
                }
            }
        }
        else
        {
            if(currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMoves(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }

            if(currentlyDragging && Input.GetMouseButtonUp(0))
            {
                currentlyDragging.SetPotisition(GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY));
                currentlyDragging = null;
                RemoveHiglightTiles();
            }
        }

        // Daca tragem o piesa 
        if (currentlyDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if(horizontalPlane.Raycast(ray,out distance))
                currentlyDragging.SetPotisition(ray.GetPoint(distance) + Vector3.up * dragOffset);
            
        }

    }
    //Creearea tablei de sah
    private void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {

        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX / 2)* tileSize, 0, (tileCountX / 2) * tileSize) + boardCenter;

        tiles = new GameObject[tileCountX, tileCountY];
        for (int x = 0; x < tileCountX; x++)
        {
            for (int y = 0; y < tileCountY; y++)
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
        }
    }
    private GameObject GenerateSingleTile(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format(":X:{0}, Y:{1}", x, y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y+1) * tileSize) - bounds;
        vertices[2] = new Vector3((x + 1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x + 1) * tileSize, yOffset, (y + 1) * tileSize) - bounds;

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };

        mesh.vertices = vertices;
        mesh.triangles = tris;

        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }

    //Creare piese
    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];

        int whiteTeam = 0,  blackTeam = 1;

        //White team
        chessPieces[0, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        chessPieces[1, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[1, 0].SetScale(Vector3.one, true);
        chessPieces[2, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[3, 0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        chessPieces[4, 0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        chessPieces[5, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[6, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[6, 0].SetScale(Vector3.one, true);
        chessPieces[7, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        for(int i = 0; i < TILE_COUNT_X; i++)
            chessPieces[i,1] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);

        //Black team
        chessPieces[0, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        chessPieces[1, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[1, 7].SetScale(Vector3.one, true);
        chessPieces[2, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[3, 7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        chessPieces[4, 7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        chessPieces[5, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[6, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[6, 7].SetScale(Vector3.one, true);
        chessPieces[7, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        for (int i = 0; i < TILE_COUNT_X; i++)
            chessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.Pawn, blackTeam);
    }
    private ChessPiece SpawnSinglePiece(ChessPieceType type, int team)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();

        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];

        return cp;
    }

    //Positioning
    private void PositionAllPieces()
    {
        for (int x = 0;x < TILE_COUNT_X;x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null)
                    PositionSinglePiece(x, y,true);
        }
    }
    private void PositionSinglePiece(int x,int y,bool force= false)
    {
        
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y;
        chessPieces[x, y].SetPotisition(GetTileCenter(x, y),force);
    }
    private Vector3 GetTileCenter(int x, int y)
    {
        return new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2);

    }
    //Tiles highlight
    private void HighlightTiles()
    {
        for(int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        }
    } 
    private void RemoveHiglightTiles()
    {
        for(int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        }

        availableMoves.Clear();
    }
    //Checkmate
    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }
    private void DisplayVictory(int winningTeam)
    {
        victoryScreen.SetActive(true);
        victoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
    }
    public void OnRematchButton()
    {
        if(localGame)
        {
            NetRematch wrm = new NetRematch();
            wrm.teamId = 0;
            wrm.wantRematch = 1;
            Client.Instance.SendToServer(wrm);

            NetRematch brm = new NetRematch();
            brm.teamId = 1;
            brm.wantRematch = 1;
            Client.Instance.SendToServer(brm);
        }
        else
        {
            NetRematch rm = new NetRematch();
            rm.teamId = currentTeam;
            rm.wantRematch = 1;
            Client.Instance.SendToServer(rm);
        }
    }
    public void GameReset()
    {
        //UI
        rematchButton.interactable = true;

        rematchIndicator.transform.GetChild(0).gameObject.SetActive(false);
        rematchIndicator.transform.GetChild(1).gameObject.SetActive(false);

        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(2).gameObject.SetActive(false);
        victoryScreen.SetActive(false);
        // Field reset
        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();
        playerRematch[0] = playerRematch[1] = false;
        //Clean up
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    Destroy(chessPieces[x, y].gameObject);

                    chessPieces[x, y] = null;
                }
            }
        }

        for (int i = 0; i < deadWhite.Count; i++)
            Destroy(deadWhite[i].gameObject);
        for (int i = 0; i < deadBlack.Count; i++)
            Destroy(deadBlack[i].gameObject);

        deadWhite.Clear();
        deadBlack.Clear();

        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = 0;
    }
    public void OnMenuButton()
    {
        NetRematch rm = new NetRematch();
        rm.teamId = currentTeam;
        rm.wantRematch = 0;
        Client.Instance.SendToServer(rm);

        GameReset();
        GameUI.Instance.OnLeaveFromGameMenu();

        Invoke("ShutDownRelay", 1.0f);

        //Reset some values
        playerCount = -1;
        currentTeam = -1;
    }

    //Special Moves
    private void ProcessSpecialMove()
    {
        if(specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] lastMove = moveList[moveList.Count-1];
            ChessPiece targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];

            if (targetPawn.type == ChessPieceType.Pawn)
            {
                if(targetPawn.team == 0 && lastMove[1].y == 7)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 0);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
                if(targetPawn.team == 1 && lastMove[1].y == 0)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 1);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
            }
        }

        if(specialMove == SpecialMove.EnPassant)
        {
            var newMove = moveList[moveList.Count-1];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];
            var targetPawnPosition = moveList[moveList.Count-2];
            ChessPiece enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];

            if(myPawn.currentX == enemyPawn.currentX)
            {
                if(myPawn.currentY == enemyPawn.currentY-1 || myPawn.currentY==enemyPawn.currentY+1)
                {
                    if(enemyPawn.team == 0)
                    {
                        deadWhite.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPotisition(new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                            - bounds
                            + new Vector3(tileSize / 2, 0, tileSize / 2)
                            + new Vector3(setX, 0, setY)
                            + (Vector3.forward * deathSpacing) * deadWhite.Count);

                    }
                    else
                    {
                        deadBlack.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPotisition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize)
                            - bounds
                            + new Vector3(tileSize / 2, 0, tileSize / 2)
                            + new Vector3(-setX, 0, -setY)
                            + (Vector3.back * deathSpacing) * deadBlack.Count);
                    }
                    chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                }
            }
        }

        if (specialMove == SpecialMove.Castling)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];

            //left rook
            if (lastMove[1].x == 2)
            {
                if(lastMove[1].y == 0) // white side
                {
                    ChessPiece rook  = chessPieces[0, 0];
                    chessPieces[3,0] = rook;
                    PositionSinglePiece(3, 0);
                    chessPieces[0, 0] = null;
                }
                else if (lastMove[1].y == 7) // black side
                {
                    ChessPiece rook = chessPieces[0, 7];
                    chessPieces[3, 7] = rook;
                    PositionSinglePiece(3, 7);
                    chessPieces[0, 7] = null;
                }
            }//right rook
            else if (lastMove[1].x == 6)
            {
                if (lastMove[1].y == 0) // white side
                {
                    ChessPiece rook = chessPieces[7, 0];
                    chessPieces[5, 0] = rook;
                    PositionSinglePiece(5, 0);
                    chessPieces[7, 0] = null;
                }
                else if (lastMove[1].y == 7) // black side
                {
                    ChessPiece rook = chessPieces[7, 7];
                    chessPieces[5, 7] = rook;
                    PositionSinglePiece(5, 7);
                    chessPieces[7, 7] = null;
                }
            }
        }
    }
    private void PreventCheck()
    {
        ChessPiece targetKing = null;
        for(int x= 0; x< TILE_COUNT_X; x++)
            for (int y= 0; y< TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null)
                    if (chessPieces[x, y].type == ChessPieceType.King)
                        if (chessPieces[x, y].team == currentlyDragging.team)              
                            targetKing = chessPieces[x, y];
                    
        // trimitem referinta la lista de mutari posibile si stergem mutarile care ar provoca sah
        SimulateMoveForSinglePiece(currentlyDragging, ref availableMoves, targetKing);
    }
    private void SimulateMoveForSinglePiece(ChessPiece cp,ref List<Vector2Int> moves,ChessPiece targetKing)
    {
        //Save current values , to reset after the function
        int acctualX = cp.currentX;
        int acctualY = cp.currentY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();

        //trece prin toate mutarile,simuleazale si verifica daca sunt sah
        for(int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPositionSim = new Vector2Int(targetKing.currentX,targetKing.currentY);
            //Verifica daca regele a fost mutat
            if(cp.type == ChessPieceType.King)
                kingPositionSim = new Vector2Int(simX,simY);

            //Copiez vectorul [,] si referinta
            ChessPiece[,] simulation = new ChessPiece[TILE_COUNT_X,TILE_COUNT_Y];
            List<ChessPiece> simAttackingPieces = new List<ChessPiece>();
            for(int x=0; x < TILE_COUNT_X; x++)
            {
                for(int y=0; y < TILE_COUNT_Y; y++)
                {
                    if (chessPieces[x,y] != null)
                    {
                        simulation[x, y] = chessPieces[x, y];
                        if (simulation[x, y].team != cp.team)
                            simAttackingPieces.Add(simulation[x, y]);
                    }
                }
            }

            //Simulare miscari
            simulation[acctualX, acctualY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX, simY] = cp;

            // S-a facut vreo capturare?
            var deadPiece = simAttackingPieces.Find(c=> c.currentX ==simX  && c.currentY ==simY);
            if (deadPiece != null)
                simAttackingPieces.Remove(deadPiece);

            //Fa rost de toate simularile mutarilor pieseleor care ataca
            List<Vector2Int> simMoves = new List<Vector2Int>();
            for(int a= 0; a < simAttackingPieces.Count; a++)
            {
                var pieceMoves = simAttackingPieces[a].GetAvailableMoves(ref simulation,TILE_COUNT_X,TILE_COUNT_Y);
                for (int b = 0; b < pieceMoves.Count; b++)
                    simMoves.Add(pieceMoves[b]);
                
            }

            //Daca regele este in sah , atunci sterge miscarea
            if (ContainsValidMoves(ref simMoves, kingPositionSim))
                movesToRemove.Add(moves[i]);

            //Restore cp data
            cp.currentX = acctualX;
            cp.currentY = acctualY;
        }

        //Sterge aceste mutari
        for (int i = 0; i < movesToRemove.Count; i++)
            moves.Remove(movesToRemove[i]);
    }
    private int CheckForChekmate()
    {
        var lastMove = moveList[moveList.Count - 1];
        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

        List<ChessPiece> attackingPieces = new List<ChessPiece>();
        List<ChessPiece> defendingPieces = new List<ChessPiece>();
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null)
                {
                    if (chessPieces[x, y].team == targetTeam)
                    {
                        defendingPieces.Add(chessPieces[x, y]);
                        if (chessPieces[x, y].type == ChessPieceType.King)
                            targetKing = chessPieces[x, y];
                    }
                    else
                    {
                        attackingPieces.Add(chessPieces[x, y]);
                    }

                }
        //E regele atacat?
        List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();
        for (int i = 0; i < attackingPieces.Count; i++)
        {
            var pieceMoves = attackingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            for (int b = 0; b < pieceMoves.Count; b++)
                currentAvailableMoves.Add(pieceMoves[b]);
        }

        //Suntem in sah acum?
        if (ContainsValidMoves(ref currentAvailableMoves, new Vector2Int(targetKing.currentX, targetKing.currentY)))
        {
            //regele e atacat, putem muta ceva sa il aparam?
            for (int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                //Stergem mutari care lasa regele in pericol
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);

                if (defendingMoves.Count != 0)
                    return 0;
            }

            return 1; // Checkmate Exit
        }
        else
        {
            for (int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);
                if (defendingMoves.Count != 0)
                    return 0;
            }
            return 2; //staleMate Exit
        }
    }
    private bool CheckForStalemate_ImposibleCheckmate()
    {
        List<ChessPiece> whitePieces = new List<ChessPiece>();
        List<ChessPiece> blackPieces = new List<ChessPiece>();

        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (chessPieces[x, y] != null)
                    if (chessPieces[x, y].team == 0)
                        whitePieces.Add(chessPieces[x, y]);
                    else
                        blackPieces.Add(chessPieces[x, y]);
        //white Pieces
        int WBishop_nr = 0;
        int WRook_nr = 0;
        int WKnight_nr = 0;
        int WQueen_nr = 0;
        int WPawn_nr = 0;
        for(int i=0;i < whitePieces.Count;i++)
        {
            if (whitePieces[i].type == ChessPieceType.Rook) WRook_nr++;
            if (whitePieces[i].type == ChessPieceType.Queen) WQueen_nr++;
            if (whitePieces[i].type == ChessPieceType.Knight) WKnight_nr++;
            if (whitePieces[i].type == ChessPieceType.Bishop) WBishop_nr++;
            if (whitePieces[i].type == ChessPieceType.Pawn) WPawn_nr++;
        }
        //Black Pieces
        int BBishop_nr = 0;
        int BRook_nr = 0;
        int BKnight_nr = 0;
        int BQueen_nr = 0;
        int BPawn_nr = 0;
        for (int i = 0; i < blackPieces.Count; i++)
        {
            if (blackPieces[i].type == ChessPieceType.Rook) BRook_nr++;
            if (blackPieces[i].type == ChessPieceType.Queen) BQueen_nr++;
            if (blackPieces[i].type == ChessPieceType.Knight) BKnight_nr++;
            if (blackPieces[i].type == ChessPieceType.Bishop) BBishop_nr++;
            if (blackPieces[i].type == ChessPieceType.Pawn) BPawn_nr++;
        }

        //Check number of pieces
        if (WQueen_nr == 0 && BQueen_nr == 0)
            if (WRook_nr == 0 && BRook_nr == 0)
                if (WPawn_nr == 0 && BPawn_nr == 0)
                    if (BBishop_nr == 0 && WBishop_nr == 0)
                        if (WKnight_nr == 0 && BKnight_nr == 0)
                            return true; // Only kings
                       else
                        {
                            if ((WKnight_nr == 0 && BKnight_nr == 1) || (WKnight_nr == 1 && BKnight_nr == 0))
                                return true; // king + knight vs king
                        }
                    else
                    {
                        if (WKnight_nr == 0 && BKnight_nr == 0)
                        {
                            if((BBishop_nr == 0 && WBishop_nr == 1) || (BBishop_nr == 1 && WBishop_nr == 0))
                                return true; // king + bishop vs king
                            else
                            {
                                if((BBishop_nr == 1 && WBishop_nr == 1))
                                {
                                    ChessPiece WBishop = whitePieces.Find(x => x.type == ChessPieceType.Bishop);
                                    ChessPiece BBishop = blackPieces.Find(x => x.type == ChessPieceType.Bishop);
                                    if(WBishop.currentX % 2 ==  BBishop.currentY % 2  && BBishop.currentX %2  == BBishop.currentY%2) 
                                        return true; // king + bishop vs king + bishop (bishops black tiles)
                                    if (WBishop.currentX % 2 != BBishop.currentY % 2 && BBishop.currentX % 2 != BBishop.currentY % 2)
                                        return true;// king + bishop vs king + bishop (bishops white tiles)
                                }
                            }
                        }
                    }
        return false;
    }
    //Operation
    private bool ContainsValidMoves(ref List<Vector2Int> moves,Vector2Int pos)
    {
        for(int i = 0; i < moves.Count; i++)
            if (moves[i].x == pos.x && moves[i].y == pos.y)
                return true;
        
        return false;
    }
    private Vector2Int TileIndex(GameObject hitInfo)
    {
        for(int x = 0; x< TILE_COUNT_X; x++)
        {
            for (int y = 0; y< TILE_COUNT_Y; y++)
                if (tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);
        }
        return -Vector2Int.one; // Nu se poate intampla 
    }
    private void MoveTo(int originalX,int originalY, int x, int y)
    {
        ChessPiece cp = chessPieces[originalX,originalY];
        Vector2Int previousPosition = new Vector2Int(originalX,originalY);

        //Is there another piece on the target position?
        if (chessPieces[x, y] != null)
        {
            ChessPiece ocp = chessPieces[x, y];

            if (cp.team == ocp.team)
                return;

            //Piesa capturara
            if (ocp.team == 0)
            {
                deadWhite.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPotisition(new Vector3(8 * tileSize, yOffset, -1 * tileSize)
                    - bounds
                    + new Vector3(tileSize / 2, 0, tileSize / 2)
                    + new Vector3(setX, 0, setY)
                    + (Vector3.forward * deathSpacing) * deadWhite.Count);
                 
            }
            else
            {
                deadBlack.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPotisition(new Vector3(-1 * tileSize, yOffset,8 * tileSize)
                    - bounds
                    + new Vector3(tileSize / 2, 0, tileSize / 2)
                    + new Vector3(-setX, 0, -setY)
                    + (Vector3.back * deathSpacing) * deadBlack.Count);
            }
        }

        chessPieces[x, y] = cp;
        chessPieces[previousPosition.x, previousPosition.y] = null;

        PositionSinglePiece(x, y);

        if (isWhiteTurn == 0)
            isWhiteTurn = 1;
        else if (isWhiteTurn == 1)
            isWhiteTurn = 0;

        if (localGame)
            currentTeam = (currentTeam == 0) ? 1 : 0;

        moveList.Add(new Vector2Int[] {previousPosition,new Vector2Int(x, y)});

        ProcessSpecialMove();

        if(currentlyDragging)
            currentlyDragging = null;
        RemoveHiglightTiles();

        switch (CheckForChekmate())
        {
            default:
                break;
            case 1:
                {
                    CheckMate(cp.team);
                    break;
                }
            case 2:
                {
                    CheckMate(2);
                    break;
                }
        }

        if (CheckForStalemate_ImposibleCheckmate())
            CheckMate(2);

        return;
    }

    #region Events
    private void RegisterEvents()
    {
        NetUtility.S_WELCOME += OnWelcomeServer;
        NetUtility.S_MAKE_MOVE += OnMakeMoveServer;
        NetUtility.S_REMATCH += OnRematchServer;

        NetUtility.C_WELCOME += OnWelcomeClient;
        NetUtility.C_START_GAME += OnStartGameClient;
        NetUtility.C_MAKE_MOVE += OnMakeMoveClient;
        NetUtility.C_REMATCH += OnRematchClient;

        GameUI.Instance.SetLocalGame += OnSetLocalGame;
    }
    private void UnRegisterEvents()
    {
        NetUtility.S_WELCOME -= OnWelcomeServer;
        NetUtility.S_MAKE_MOVE -= OnMakeMoveServer;
        NetUtility.S_REMATCH -= OnRematchServer;

        NetUtility.C_WELCOME -= OnWelcomeClient;
        NetUtility.C_START_GAME -= OnStartGameClient;
        NetUtility.C_MAKE_MOVE -= OnMakeMoveClient;
        NetUtility.C_REMATCH -= OnRematchClient;

        GameUI.Instance.SetLocalGame -= OnSetLocalGame;
    }

    //Server
    private void OnWelcomeServer(NetMessage msg, NetworkConnection cnn)
    {
        //Client has connected , assign a team and return the message back to him
        NetWelcome nw = msg as NetWelcome;

        //Assign team
        nw.AssignedTeam = ++playerCount;

        //Return back to the client
        Server.Instance.SendToClient(cnn, nw);

        //If full start the game
        if(playerCount == 1)
        {
            Server.Instance.BroadCast(new NetStartGame());
        }
    }
    private void OnMakeMoveServer(NetMessage msg, NetworkConnection cnn)
    {
        NetMakeMove nm = msg as NetMakeMove;

        // This where you could do some validation codes !
        //--

        //Receive and broadcast back
        Server.Instance.BroadCast(nm);
    }
    private void OnRematchServer(NetMessage msg, NetworkConnection cnn)
    {
        Server.Instance.BroadCast(msg);
    }

    //Client
    private void OnWelcomeClient(NetMessage msg)
    {
        //Receive the connection mesage 
        NetWelcome nw = msg as NetWelcome;

        //Asign team
        currentTeam = nw.AssignedTeam;

        Debug.Log($"My assinged team is {nw.AssignedTeam}");

        if (localGame && currentTeam == 0)
        {
            Server.Instance.BroadCast(new NetStartGame());
        }
    }
    private void OnStartGameClient(NetMessage msg)
    {
        // We just need to change the camera
        GameUI.Instance.ChangeCamera((currentTeam == 0) ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
    }
    private void OnMakeMoveClient(NetMessage msg)
    {
        NetMakeMove mm = msg as NetMakeMove;

        Debug.Log($"MM : {mm.teamId} : {mm.originalX} {mm.originalY} -> {mm.destinationX} {mm.destinationY}");

        if(mm.teamId != currentTeam)
        {
            ChessPiece target = chessPieces[mm.originalX,mm.originalY];

            availableMoves = target.GetAvailableMoves(ref chessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            specialMove = target.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

            MoveTo(mm.originalX,mm.originalY,mm.destinationX,mm.destinationY);
        }
    }
    private void OnRematchClient(NetMessage msg)
    {
        //Recieve connection message
        NetRematch rm = msg as NetRematch;
        //Set the boolean for rematch
        playerRematch[rm.teamId] = rm.wantRematch == 1;
        //Activate piece of Ui
        if(rm.teamId != currentTeam)
        {
            rematchIndicator.transform.GetChild((rm.wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);
            if(rm.wantRematch != 1)
            {
                rematchButton.interactable = false;
            }
        }
        //If both want rematch
        if (playerRematch[0] && playerRematch[1])
            GameReset();
    }

    //

    private void ShutDownRelay(float s)
    {
        Client.Instance.ShutDown();
        Server.Instance.ShutDown();
    }
    private void OnSetLocalGame(bool v)
    {
        playerCount = -1;
        currentTeam = -1;
        localGame = v;
    }
    #endregion
}