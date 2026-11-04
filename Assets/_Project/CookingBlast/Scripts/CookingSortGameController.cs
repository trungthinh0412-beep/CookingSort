using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CookingSort2D
{
    public class CookingSortGameController : MonoBehaviour
    {
        [Header("Scene pieces")]
        [SerializeField] private BoardController board;
        [SerializeField] private SeatBar seatBar;
        [SerializeField] private CustomerQueue customerQueue;
        private PopupInGame hud;
        [SerializeField] private CookingBlastLevel levelDefinition;

        private int _servedTiles;
        private int _combo;
        
        private CookingBlastLevelLoader _levelLoader;
        private readonly Dictionary<FoodColor, int> _bottomTypeWeights =
            new Dictionary<FoodColor, int>();
        private BoosterType? _activeBooster;
        private CustomerView _lastSeatedCustomer;
        private int _lastSeatLane = -1;
        private int _lastSeatRow = -1;

        public event System.Action LevelWon;
        public event System.Action LevelFailed;
private bool _gameEnded;

        public CookingBlastLevel LevelDefinition => levelDefinition;
        public BoardController Board => board;
        public bool GameEnded => _gameEnded;

        /// <summary>
        /// Fills a booster target row using the gameplay layout convention:
        /// row 1 = animals assigned to SeatBar,
        /// row 2 = front customer in every queue lane,
        /// row 3 = second customer in every queue lane.
        /// </summary>
        public void FillBoosterRow(int rowNumber, List<CustomerView> results)
        {
            if (results == null) return;
            results.Clear();

            if (rowNumber == 1)
            {
                if (seatBar != null) seatBar.GetBoosterRow(results);
                return;
            }

            if (rowNumber == 2 || rowNumber == 3)
            {
                if (customerQueue != null)
                    customerQueue.GetBoosterRow(rowNumber - 2, results);
            }
        }

private void Start()
        {
            _levelLoader = FindFirstObjectByType<CookingBlastLevelLoader>();
            if (_levelLoader == null && levelDefinition != null)
                StartLevel(levelDefinition.LevelNumber);
        }

public void StartLevel(int displayLevelNumber = -1)
        {
            if (levelDefinition == null)
            {
                Debug.LogError("[CookingBlast] Missing CookingBlastLevel instance on GameplayRoot.");
                return;
            }

            if (!levelDefinition.TryValidate(out string error))
            {
                Debug.LogError($"[CookingBlast] Invalid level '{levelDefinition.name}': {error}");
                return;
            }

            board = levelDefinition.Board;
            seatBar = levelDefinition.SeatBar;
            customerQueue = levelDefinition.CustomerQueue;
            if (board == null || seatBar == null || customerQueue == null)
            {
                Debug.LogError($"[CookingBlast] Level '{levelDefinition.name}' is missing Board / SeatBar / CustomerQueue scene pieces.");
                return;
            }

            _servedTiles = 0;
            _combo = 0;
            _gameEnded = false;
            _activeBooster = null;
            ClearRewindRecord();
            Observer.ActiveBoosterChanged?.Invoke(null);

            seatBar.Initialize(this, levelDefinition.SeatCount);
            board.Initialize(this, levelDefinition);
            customerQueue.Initialize(this, levelDefinition);
            int shownLevel = displayLevelNumber > 0
                ? displayLevelNumber
                : levelDefinition.LevelNumber;
            if (PopupController.Instance != null)
            {
                hud = PopupController.Instance.Get<PopupInGame>() as PopupInGame;
                PopupController.Instance.Show<PopupInGame>(PopupAnimation.None);
            }

            if (hud != null)
                hud.Initialize(this, shownLevel, levelDefinition.TotalTileCount);
        }

public void SetLevelDefinition(CookingBlastLevel level)
        {
            levelDefinition = level;
        }


        public bool TrySeatCustomer(CustomerView customer, int sourceLane = -1, int sourceRow = -1)
        {
            if (_gameEnded || customer == null || seatBar == null || board == null) return false;
            if (board.IsIntroAnimating) return false;
            if (!seatBar.TrySeat(customer)) return false;

            _lastSeatedCustomer = customer;
            _lastSeatLane = sourceLane;
            _lastSeatRow = sourceRow;

            EvaluateState();
            return true;
        }

        public bool TryUseBooster(BoosterType type)
        {
            if (!CanUseBoosters()) return false;
            if (GetBoosterAmount(type) <= 0)
            {
                Notify("No booster left.");
                return false;
            }

            switch (type)
            {
                case BoosterType.OrderBell:
                    if (seatBar == null || seatBar.IsFull)
                    {
                        Notify("The seat row is full.");
                        return false;
                    }
                    SetActiveBooster(type);
                    Notify("Choose an animal from row 2 or below.");
                    return true;

                case BoosterType.LineUp:
                    board.GetBottomTypeWeights(_bottomTypeWeights);
                    if (customerQueue == null ||
                        !customerQueue.ShuffleWaitingCustomers(_bottomTypeWeights))
                    {
                        Notify("Not enough waiting animals to line up.");
                        return false;
                    }
                    CompleteBooster(type);
                    return true;

                case BoosterType.Rewind:
                    return TryRewind();

                case BoosterType.TrashAway:
                    SetActiveBooster(type);
                    Notify("Choose a connected food group on the board.");
                    return true;
            }
            return false;
        }

        public bool HandleBoosterCustomerClick(CustomerView customer)
        {
            if (_activeBooster != BoosterType.OrderBell) return false;
            if (!CanUseBoosters() || customerQueue == null ||
                !customerQueue.TryPromoteWithOrderBell(customer))
            {
                Notify("That animal cannot move to the seat row now.");
                return true;
            }
            CompleteBooster(BoosterType.OrderBell);
            return true;
        }

        public bool HandleBoosterTileClick(FoodTileView tile)
        {
            if (_activeBooster != BoosterType.TrashAway) return false;
            if (!CanUseBoosters() || board == null || !board.TryTrashConnected(tile))
            {
                Notify("Choose a connected food group on the board.");
                return true;
            }
            CompleteBooster(BoosterType.TrashAway);
            return true;
        }

        public void CancelActiveBooster()
        {
            SetActiveBooster(null);
        }

        private bool TryRewind()
        {
            if (_lastSeatedCustomer == null || _lastSeatLane < 0 ||
                seatBar == null || seatBar.OccupiedCount < 2)
            {
                Notify("Rewind needs at least 2 seated animals and a previous move.");
                return false;
            }
            if (board != null && board.HasTileInFlight(_lastSeatedCustomer))
            {
                Notify("Wait for the flying food before rewinding.");
                return false;
            }
            CustomerView customer = _lastSeatedCustomer;
            if (!seatBar.TryDetachForRewind(customer) || customerQueue == null ||
                !customerQueue.RestoreCustomer(customer, _lastSeatLane, _lastSeatRow))
            {
                Notify("The previous animal can no longer be rewound.");
                return false;
            }
            CompleteBooster(BoosterType.Rewind);
            EvaluateState();
            return true;
        }

        private bool CanUseBoosters()
        {
            if (_gameEnded || board == null || seatBar == null || customerQueue == null)
                return false;
            return true;
        }

        private void CompleteBooster(BoosterType type)
        {
            ConsumeBooster(type);
            SetActiveBooster(null);
            // Any booster use breaks the normal placement history. Rewind itself
            // also consumes the one move it restored.
            ClearRewindRecord();
        }

        private void SetActiveBooster(BoosterType? type)
        {
            if (customerQueue != null)
                customerQueue.SetOrderBellSelection(type == BoosterType.OrderBell);
            _activeBooster = type;
            Observer.ActiveBoosterChanged?.Invoke(type);
        }

        private void ClearRewindRecord()
        {
            _lastSeatedCustomer = null;
            _lastSeatLane = -1;
            _lastSeatRow = -1;
        }

        private static int GetBoosterAmount(BoosterType type)
        {
            if (Data.PlayerData == null) return 0;
            return type switch
            {
                BoosterType.OrderBell => Data.PlayerData.CurrentOrderBell,
                BoosterType.LineUp => Data.PlayerData.CurrentLineUp,
                BoosterType.Rewind => Data.PlayerData.CurrentRewind,
                BoosterType.TrashAway => Data.PlayerData.CurrentTrashAway,
                _ => 0
            };
        }

        private static void ConsumeBooster(BoosterType type)
        {
            if (Data.PlayerData == null) return;
            switch (type)
            {
                case BoosterType.OrderBell:
                    Data.PlayerData.CurrentOrderBell--;
                    break;
                case BoosterType.LineUp:
                    Data.PlayerData.CurrentLineUp--;
                    break;
                case BoosterType.Rewind:
                    Data.PlayerData.CurrentRewind--;
                    break;
                case BoosterType.TrashAway:
                    Data.PlayerData.CurrentTrashAway--;
                    break;
            }
            Data.SaveData();
        }

        private static void Notify(string message)
        {
            Observer.Notify?.Invoke(message, Vector3.zero);
        }

        public void OnTrashAwayFinished(int removedTiles, FoodColor type)
        {
            if (_gameEnded || removedTiles <= 0) return;
            _servedTiles += removedTiles;
            if (hud != null) hud.SetProgress(_servedTiles);
            if (board != null && seatBar != null) board.BeginServeLoop(seatBar);
            EvaluateState();
        }

        public void OnCustomerSeatFlightFinished()
        {
            if (_gameEnded || board == null || seatBar == null) return;
            if (!seatBar.IsMerging)
                board.BeginServeLoop(seatBar);
            EvaluateState();
        }

        public void OnTileLaunched()
        {
            if (_gameEnded) return;
            _combo++;
            if (hud != null) hud.SetCombo(_combo, GetComboMultiplier(_combo));
        }

        public void OnTileServed(FoodColor color)
        {
            if (_gameEnded) return;
            _servedTiles++;
            if (hud != null) hud.SetProgress(_servedTiles);
        }

        public void OnServeLoopFinished()
        {
            EvaluateState();
        }

        public void OnCustomerLeftSeat()
        {
            EvaluateState();
        }

public void OnSeatMergeFinished()
        {
            if (_gameEnded) return;
            if (board != null && seatBar != null)
                board.BeginServeLoop(seatBar);
            EvaluateState();
        }


public void RestartLevel()
        {
            if (_levelLoader == null) _levelLoader = FindFirstObjectByType<CookingBlastLevelLoader>();
            if (_levelLoader != null)
            {
                _levelLoader.ReloadCurrentLevel();
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.name);
        }

public void ContinueAfterWin()
        {
            if (_levelLoader == null) _levelLoader = FindFirstObjectByType<CookingBlastLevelLoader>();
            if (_levelLoader != null)
            {
                _levelLoader.CompleteAndLoadNextLevel();
                return;
            }
            RestartLevel();
        }


        private void EvaluateState()
        {
            if (_gameEnded || board == null || seatBar == null) return;
            if (board.IsIntroAnimating) return;

            // Airborne tiles have left the board but are not served yet.
            // Wait for the scan pipeline and any merge before deciding win/fail.
            if (board.IsServing || seatBar.IsMerging || seatBar.IsSeating ||
                seatBar.HasDepartingCustomers) return;

            if (board.IsEmpty)
            {
                _gameEnded = true;
                if (hud != null) hud.ShowWin();
                LevelWon?.Invoke();
                return;
            }

            if (seatBar.IsMerging || board.IsServing) return;

            bool canServe = board.HasServiceableTile(seatBar);
            bool queueEmpty = customerQueue == null || !customerQueue.HasWaitingCustomers;
            if ((seatBar.IsFull && !canServe) || (queueEmpty && !canServe))
            {
                _gameEnded = true;
                _combo = 0;
                if (hud != null)
                {
                    hud.SetCombo(0, 1f);
                    hud.ShowFail();
                }
                LevelFailed?.Invoke();
            }
        }

        private float GetComboMultiplier(int combo)
        {
            if (combo >= 300) return 3f;
            if (combo >= 200) return 2f;
            if (combo >= 100) return 1.5f;
            if (combo >= 30) return 1.2f;
            return 1f;
        }
    }
}
