// Module 4 | Sentinels | University of Moratuwa | 2026

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TeamSentinels.Module4.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TeamSentinels.Module4.UI
{
    /// <summary>
    /// Data-visualisation replay panel. Scrubs through ReplayFrames and MissionEvents,
    /// displaying NPC states at the current replay time without affecting the 3D scene.
    /// </summary>
    public class ReplayControlPanel : MonoBehaviour
    {
        #region Data

        [Header("Scrub Controls")]
        [SerializeField] private Slider scrubBar;
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Button rewindButton;
        [SerializeField] private Text   currentTimeText;
        [SerializeField] private Text   playbackSpeedText;

        [Header("Speed Buttons")]
        [SerializeField] private Button speed05xButton;
        [SerializeField] private Button speed1xButton;
        [SerializeField] private Button speed2xButton;

        [Header("Actor State Board")]
        [SerializeField] private RectTransform actorStateBoard;
        [SerializeField] private GameObject    actorStateLabelPrefab;

        private List<ReplayFrame>  _frames        = new List<ReplayFrame>();
        private List<MissionEvent> _events        = new List<MissionEvent>();
        private float              _totalDuration = 1f;
        private float              _currentTime;
        private float              _playbackSpeed = 1f;
        private bool               _isPlaying;

        private Coroutine          _playCoroutine;

        // Cache actor state labels to avoid destroying/recreating them every tick
        private readonly Dictionary<string, Text> _actorLabels = new Dictionary<string, Text>();

        private static readonly Color _playColor  = Color.green;
        private static readonly Color _pauseColor = Color.yellow;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            scrubBar?.onValueChanged.AddListener(OnScrubChanged);
            playPauseButton?.onClick.AddListener(TogglePlayPause);
            rewindButton?.onClick.AddListener(RewindTenSeconds);
            speed05xButton?.onClick.AddListener(() => SetSpeed(0.5f));
            speed1xButton? .onClick.AddListener(() => SetSpeed(1.0f));
            speed2xButton? .onClick.AddListener(() => SetSpeed(2.0f));
        }

        private void OnDestroy()
        {
            scrubBar?.onValueChanged.RemoveAllListeners();
            playPauseButton?.onClick.RemoveAllListeners();
            rewindButton?.onClick.RemoveAllListeners();
            speed05xButton?.onClick.RemoveAllListeners();
            speed1xButton? .onClick.RemoveAllListeners();
            speed2xButton? .onClick.RemoveAllListeners();
        }

        #endregion

        #region Public API

        /// <summary>Loads session data and resets the replay to t=0.</summary>
        public void Populate(List<ReplayFrame> frames, List<MissionEvent> events, float totalDuration)
        {
            _frames        = frames        ?? new List<ReplayFrame>();
            _events        = events        ?? new List<MissionEvent>();
            _totalDuration = Mathf.Max(totalDuration, 1f);
            _currentTime   = 0f;
            _isPlaying     = false;

            if (scrubBar != null)
            {
                scrubBar.minValue = 0f;
                scrubBar.maxValue = _totalDuration;
                scrubBar.value    = 0f;
            }

            SetSpeed(1f);
            UpdateTimeDisplay();
            RefreshActorBoard(0f);
        }

        #endregion

        #region Private — Playback

        private void TogglePlayPause()
        {
            _isPlaying = !_isPlaying;

            if (_playCoroutine != null) StopCoroutine(_playCoroutine);
            if (_isPlaying)
                _playCoroutine = StartCoroutine(PlayCoroutine());

            UpdatePlayPauseLabel();
        }

        private void RewindTenSeconds()
        {
            SeekTo(Mathf.Max(_currentTime - 10f, 0f));
        }

        private void SetSpeed(float speed)
        {
            _playbackSpeed = speed;
            if (playbackSpeedText != null)
                playbackSpeedText.text = speed == 0.5f ? "0.5x" : $"{speed:F0}x";
        }

        private IEnumerator PlayCoroutine()
        {
            while (_isPlaying && _currentTime < _totalDuration)
            {
                _currentTime = Mathf.Min(_currentTime + Time.deltaTime * _playbackSpeed, _totalDuration);
                SyncScrubBar();
                UpdateTimeDisplay();
                RefreshActorBoard(_currentTime);

                if (_currentTime >= _totalDuration)
                {
                    _isPlaying = false;
                    UpdatePlayPauseLabel();
                }

                yield return null;
            }
        }

        private void OnScrubChanged(float value)
        {
            // Only respond to user drag, not programmatic updates
            SeekTo(value);
        }

        private void SeekTo(float time)
        {
            _currentTime = Mathf.Clamp(time, 0f, _totalDuration);
            SyncScrubBar();
            UpdateTimeDisplay();
            RefreshActorBoard(_currentTime);
        }

        private void SyncScrubBar()
        {
            if (scrubBar == null) return;
            scrubBar.onValueChanged.RemoveListener(OnScrubChanged);
            scrubBar.value = _currentTime;
            scrubBar.onValueChanged.AddListener(OnScrubChanged);
        }

        private void UpdatePlayPauseLabel()
        {
            var btn = playPauseButton?.GetComponentInChildren<Text>();
            if (btn != null)
            {
                btn.text  = _isPlaying ? "PAUSE" : "PLAY";
                btn.color = _isPlaying ? _pauseColor : _playColor;
            }
        }

        private void UpdateTimeDisplay()
        {
            if (currentTimeText == null) return;
            currentTimeText.text = $"{FormatTime(_currentTime)} / {FormatTime(_totalDuration)}";
        }

        #endregion

        #region Private — Actor Board

        private void RefreshActorBoard(float currentTime)
        {
            if (actorStateBoard == null || _frames.Count == 0) return;

            // For each actor, find the latest frame at or before currentTime
            var latest = _frames
                .Where(f => f.timestamp <= currentTime)
                .GroupBy(f => f.actorId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.timestamp).First());

            foreach (var kv in latest)
            {
                string actorId = kv.Key;
                string state   = kv.Value.currentState ?? "Unknown";
                string label   = $"{actorId}: {state}";

                if (_actorLabels.TryGetValue(actorId, out Text existing))
                {
                    if (existing != null) existing.text = label;
                }
                else
                {
                    // Create a new label row
                    GameObject row = actorStateLabelPrefab != null
                        ? Instantiate(actorStateLabelPrefab, actorStateBoard)
                        : CreateDefaultLabel(actorStateBoard);

                    var t = row.GetComponentInChildren<Text>();
                    if (t != null)
                    {
                        t.text  = label;
                        t.color = Color.white;
                    }
                    _actorLabels[actorId] = t;
                }
            }
        }

        private static GameObject CreateDefaultLabel(RectTransform parent)
        {
            var go = new GameObject("ActorLabel", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.fontSize  = 18;
            t.color     = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            return go;
        }

        #endregion

        #region Private — Helpers

        private static string FormatTime(float seconds)
        {
            int m = (int)(seconds / 60f);
            int s = (int)(seconds % 60f);
            return $"{m:D2}:{s:D2}";
        }

        #endregion
    }
}
