using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace AmmoVariants
{
    public sealed class AmmoBallistaTrail : MonoBehaviour
    {
        internal static readonly string[] AmmoNames = { "TurretBoltWood","TurretBoltBone","TurretBolt","TurretBoltFlametal","TurretBoltBloodgold" };
        private static readonly Dictionary<string,string> Labels = new Dictionary<string,string>();
        private static readonly HashSet<string> Logged = new HashSet<string>();
        private static readonly int TintKey = "AmmoVariants_ballista_ammo".GetStableHashCode();
        private const float WidthShare = 0.07f;
        private const float MinimumWidth = 0.12f;
        private const float MaximumWidth = 0.22f;
        private const float Opacity = 0.6f;
        private const float CleanupMargin = 0.1f;
        private string ammoName;
        private bool started;
        private bool finished;
        private bool receivedTint;
        private float elapsed;
        private TrailRenderer trail;
        private AmmoTrailDissolve path;
        private Projectile projectile;
        private ZNetView view;

        internal static void Register()
        {
            foreach (var name in AmmoNames)
            {
                var item = PrefabManager.Instance.GetPrefab(name)?.GetComponent<ItemDrop>();
                var shared = item?.m_itemData.m_shared;
                var source = shared?.m_attack?.m_attackProjectile;
                if (source == null) continue;
                Labels[shared.m_name] = name;
                if (AmmoTrailSettings.Ballista && source.GetComponent<AmmoBallistaTrail>() == null) source.AddComponent<AmmoBallistaTrail>();
            }
        }

        internal static void Attach(Projectile projectile, ItemDrop.ItemData ammo)
        {
            if (ammo?.m_shared?.m_ammoType != "$ammo_turretbolt") return;
            string name = ammo.m_dropPrefab != null ? ammo.m_dropPrefab.name : null;
            if (name == null || System.Array.IndexOf(AmmoNames,name) < 0)
                if (!Labels.TryGetValue(ammo.m_shared.m_name,out name)) return;
            var view = projectile.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.IsOwner()) view.GetZDO().Set(TintKey,name);
            if (!AmmoTrailSettings.Ballista) return;
            var effect = projectile.GetComponent<AmmoBallistaTrail>();
            if (effect == null) effect = projectile.gameObject.AddComponent<AmmoBallistaTrail>();
            effect.ammoName = name;
            effect.receivedTint = true;
            effect.view = view;
            effect.Begin();
        }

        private Color ColorOf()
        {
            if (ammoName == "TurretBoltFlametal") return new Color32(207,96,65,255);
            string tier = ammoName == "TurretBolt" ? "Blackmetal" : ammoName == "TurretBoltBloodgold" ? "BloodGold" : ammoName == "TurretBoltBone" ? "Bone" : "Wood";
            return AmmoPalette.MaterialColor(tier);
        }

        // the trail is built once the bolt's material is known, with the same path, hold and fade as the piercing trail.
        internal void Begin()
        {
            if (started || finished || !receivedTint || !AmmoTrailSettings.Ballista) return;
            projectile = GetComponent<Projectile>();
            if (projectile != null && Shots.FlightEnded(projectile)) return;
            started = true;
            var root = new GameObject("AmmoBallistaPath");
            root.transform.position = transform.position;
            trail = root.AddComponent<TrailRenderer>();
            var size = AmmoModels.BoundsOf(gameObject).size;
            var start = ColorOf(); start.a = Opacity;
            var end = new Color(start.r,start.g,start.b,0f);
            var middle = start; middle.a *= 0.7f;
            trail.startWidth = Mathf.Clamp(Mathf.Max(size.x,Mathf.Max(size.y,size.z)) * WidthShare,MinimumWidth,MaximumWidth);
            trail.endWidth = 0f;
            trail.startColor = start;
            trail.endColor = end;
            trail.colorGradient = AmmoVisuals.Gradient(start,middle,end);
            trail.sharedMaterial = AmmoVisuals.TrailMaterial();
            trail.minVertexDistance = AmmoVisuals.MinimumDistance;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            trail.enabled = true;
            trail.Clear();
            trail.AddPosition(transform.position);
            path = root.AddComponent<AmmoTrailDissolve>();
            path.Prepare(trail,null);
            if (Plugin.LogCounters.Value && Logged.Add(ammoName ?? name))
                Plugin.Log.LogInfo($"ballista visual trail started {ammoName ?? name}, width {trail.startWidth:F3}, opacity {Opacity:F2}, hold {AmmoVisuals.TrailHoldSeconds:F2}, impact fade {AmmoVisuals.PiercingFadeSeconds:F2}, mechanics unchanged");
        }

        private void LateUpdate()
        {
            if (finished) return;
            if (view == null) view = GetComponent<ZNetView>();
            if (!receivedTint && view != null && view.IsValid())
            {
                var value = view.GetZDO().GetString(TintKey,"");
                if (System.Array.IndexOf(AmmoNames,value) >= 0) { ammoName = value; receivedTint = true; }
            }
            if (!started)
            {
                if (receivedTint && view != null && view.IsValid()) Begin();
                return;
            }
            if (trail == null) return;
            if (projectile != null && Shots.FlightEnded(projectile)) { Release(); return; }
            trail.transform.position = transform.position;
            elapsed += Time.deltaTime;
            trail.time = elapsed + AmmoVisuals.FadeSeconds + CleanupMargin;
            if (trail.positionCount == 0 || Vector3.Distance(trail.GetPosition(trail.positionCount - 1),transform.position) >= AmmoVisuals.MinimumDistance)
                path.RecordPoint(transform.position);
        }

        internal void Release()
        {
            if (finished) return;
            finished = true;
            if (trail == null || path == null) return;
            if (trail.positionCount == 0 || Vector3.Distance(trail.GetPosition(trail.positionCount - 1),transform.position) >= AmmoVisuals.MinimumDistance)
                path.RecordPoint(transform.position);
            trail.emitting = false;
            path.Release();
            Destroy(trail.gameObject,AmmoVisuals.PiercingFadeSeconds + CleanupMargin);
            trail = null;
        }

        private void OnDisable()
        {
            if (gameObject.scene.isLoaded) Release();
        }
    }
}
