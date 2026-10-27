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
        [SerializeField] private GameHud hud;
        [SerializeField] private CookingBlastLevel levelDefinition;

        private int _servedTiles;
        private int _combo;
        
        private CookingBlastLevelLoader _levelLoader;

        public event System.Action LevelWon;
        public event System.Action LevelFailed;
private bool _gameEnded;

        public CookingBlastLevel LevelDefinition => levelDefinition;
        public BoardController Board => board;
        public bool GameEnded => _gameEnded;

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

            seatBar.Initialize(this, levelDefinition.SeatCount);
            board.Initialize(this, levelDefinition);
            customerQueue.Initialize(this, levelDefinition);
            int shownLevel = displayLevelNumber > 0 ? displayLevelNumber : levelDefinition.LevelNumber;
            if (hud != null) hud.Initialize(this, shownLevel, levelDefinition.TotalTileCount);
            if (PopupController.Instance != null)
                PopupController.Instance.Show<PopupInGame>(PopupAnimation.None);
        }

public void SetLevelDefinition(CookingBlastLevel level)
        {
            levelDefinition = level;
        }


        public bool TrySeatCustomer(CustomerView customer)
        {
            if (_gameEnded || customer == null || seatBar == null || board == null) return false;
            if (!seatBar.TrySeat(customer)) return false;

            EvaluateState();
            return true;
        }

        public void OnCustomerSeatFlightFinished()
        {
            if (_gameEnded || board == null || seatBar == null) return;
            if (!seatBar.IsMerging)
                board.BeginServeLoop(seatBar);
            EvaluateState();
        }

        public void OnTileServed(FoodColor color)
        {
            if (_gameEnded) return;
            _servedTiles++;
            _combo++;
            if (hud != null)
            {
                hud.SetProgress(_servedTiles);
                hud.SetCombo(_combo, GetComboMultiplier(_combo));
            }
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
            if (combo >= 180) return 3f;
            if (combo >= 100) return 2f;
            if (combo >= 50) return 1.5f;
            if (combo >= 20) return 1.2f;
            return 1f;
        }
    }
}
