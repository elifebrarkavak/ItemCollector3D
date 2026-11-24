using ItemCollector3D.Menus;
using ItemCollector3D.Treasure;
using ItemCollector3D.World;
using UnityEngine;

namespace ItemCollector3D.UI
{
    /// <summary>
    /// "DESERT OUTPOST", "DRY RIVERBED" ...: a small banner (the existing <see cref="NotificationBanner"/> pop / hold / fade, a second instance placed under the HUD's top bar) shown for about
    /// 2.4 seconds when the player enters a new exploration area of Rocky Canyon or the Ancient Mine (stage 16D). It listens to <see cref="CanyonZoneTracker.ZoneEntered"/> or <see cref="MineZoneTracker.ZoneEntered"/> and keeps itself out of the way:
    /// it waits until the player has stayed in the new area for <see cref="DwellSeconds"/> (a quick step across a border shows nothing), never shows while the game is paused or a dig runs or
    /// while the progress banner (AREA COMPLETE / NEW AREA UNLOCKED) is up, never shows two banners closer than <see cref="MinimumGapSeconds"/>, and never repeats the same area within
    /// <see cref="RepeatCooldownSeconds"/>. The banner takes no touches. Without audio or save access: it only reads the tracker.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZoneEntryNotice : MonoBehaviour
    {
        public const float DwellSeconds = 0.6f;
        public const float MinimumGapSeconds = 3f;
        public const float RepeatCooldownSeconds = 45f;
        public const float GiveUpAfterSeconds = 10f;

        [SerializeField] private CanyonZoneTracker zones;
        [Tooltip("The Ancient Mine's tracker (used instead of the canyon one in that scene).")]
        [SerializeField] private MineZoneTracker mineZones;
        [SerializeField] private NotificationBanner banner;
        [Tooltip("The progress banner (AREA COMPLETE ...): a zone name waits while it is showing. Optional.")]
        [SerializeField] private NotificationBanner progressBanner;
        [SerializeField] private TreasureHuntController hunt;
        [SerializeField] private Color color = new(0.98f, 0.86f, 0.55f, 1f);

        private readonly float[] lastShown = { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };
        private bool hasPending;
        private int pending;
        private string pendingName;
        private float pendingSince;
        private float lastBannerTime = float.NegativeInfinity;

        /// <summary>The banner that shows the zone names.</summary>
        public NotificationBanner Banner => banner;

        /// <summary>How many zone banners were shown so far.</summary>
        public int ShownCount { get; private set; }

        public bool HasPending => hasPending;

        private void Awake()
        {
            if (zones == null && mineZones == null)
            {
                zones = FindAnyObjectByType<CanyonZoneTracker>();
                if (zones == null)
                {
                    mineZones = FindAnyObjectByType<MineZoneTracker>();
                }
            }

            if (hunt == null)
            {
                hunt = FindAnyObjectByType<TreasureHuntController>();
            }
        }

        private void OnEnable()
        {
            if (zones != null)
            {
                zones.ZoneEntered += HandleCanyonEntered;
                if (zones.HasZone)
                {
                    HandleCanyonEntered(zones.Current);
                }
            }

            if (mineZones != null)
            {
                mineZones.ZoneEntered += HandleMineEntered;
                if (mineZones.HasZone)
                {
                    HandleMineEntered(mineZones.Current);
                }
            }
        }

        private void OnDisable()
        {
            if (zones != null)
            {
                zones.ZoneEntered -= HandleCanyonEntered;
            }

            if (mineZones != null)
            {
                mineZones.ZoneEntered -= HandleMineEntered;
            }
        }

        private void HandleCanyonEntered(CanyonZone zone) => HandleEntered((int)zone, CanyonZoneTracker.DisplayName(zone));

        private void HandleMineEntered(MineZone zone) => HandleEntered((int)zone, MineZoneTracker.DisplayName(zone));

        private void HandleEntered(int zone, string displayName)
        {
            hasPending = true;
            pending = zone;
            pendingName = displayName;
            pendingSince = Time.unscaledTime;
        }

        private void Update()
        {
            if (!hasPending || banner == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - pendingSince > GiveUpAfterSeconds + DwellSeconds)
            {
                hasPending = false; // the moment has passed
                return;
            }

            // the player is no longer in the area that was entered (it was only a step across the border): forget it
            if ((zones != null && zones.HasZone && (int)zones.Current != pending) || (mineZones != null && mineZones.HasZone && (int)mineZones.Current != pending))
            {
                return; // HandleEntered replaced the pending zone already; nothing else to do
            }

            if (now - pendingSince < DwellSeconds
                || GamePause.IsPaused
                || banner.IsShowing
                || (progressBanner != null && progressBanner.IsShowing)
                || (hunt != null && hunt.IsDigging)
                || now - lastBannerTime < MinimumGapSeconds)
            {
                return;
            }

            hasPending = false;
            if (now - lastShown[pending] < RepeatCooldownSeconds)
            {
                return;
            }

            lastShown[pending] = now;
            lastBannerTime = now;
            ShownCount++;
            banner.Show(pendingName, string.Empty, color);
        }
    }
}
