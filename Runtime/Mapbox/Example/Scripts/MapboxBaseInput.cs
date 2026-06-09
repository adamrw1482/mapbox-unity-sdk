using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Mapbox.Example.Scripts.MapInput
{
	/// <summary>
	/// <c>BaseInput</c> override that routes UGUI EventSystem reads through
	/// <see cref="IPointerInput"/>. <c>StandaloneInputModule</c> auto-detects
	/// a <c>BaseInput</c> sibling and uses it instead of <c>UnityEngine.Input.*</c>,
	/// so the legacy module keeps working under Active Input Handling = "Old" /
	/// "New" / "Both" without users having to swap to <c>InputSystemUIInputModule</c>.
	///
	/// Auto-attached by <see cref="EnsureAttachedToEventSystem"/> on map init —
	/// demo scenes never need a hand-edited component, and any user scene that
	/// uses the SDK gets the same protection.
	///
	/// Every virtual member of <c>BaseInput</c> is overridden — leaving any
	/// member to the default impl would let UGUI's EventSystem read through
	/// <c>UnityEngine.Input.*</c> and throw under "New"-only mode (e.g.
	/// <c>StandaloneInputModule</c> reads <c>mouseScrollDelta</c> every frame
	/// in <c>GetMousePointerEventData</c>).
	///
	/// The yellow "Replace with InputSystemUIInputModule" warning on
	/// <c>StandaloneInputModule</c> still appears in the Inspector under
	/// New / Both mode — it's a cosmetic nag, runtime is unaffected.
	/// </summary>
	[AddComponentMenu("Event/Mapbox Base Input")]
	public sealed class MapboxBaseInput : BaseInput
	{
		// Bootstraps the BaseInput override for every EventSystem in every scene.
		// AfterSceneLoad fires after Awake/OnEnable but before the first Update,
		// which is when EventSystem.Update first calls StandaloneInputModule.Process()
		// — i.e. the first frame any Input.* read would otherwise throw. Subscribing
		// to SceneManager.sceneLoaded handles subsequent scene loads (additive
		// and single).
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Bootstrap()
		{
			EnsureAttachedToAllEventSystems();
			SceneManager.sceneLoaded += (scene, mode) => EnsureAttachedToAllEventSystems();
		}

		/// <summary>
		/// Idempotently attaches <see cref="MapboxBaseInput"/> to every <c>EventSystem</c>
		/// in the active scene(s) and wires it as the <c>inputOverride</c> on every
		/// <see cref="BaseInputModule"/> on that GameObject. Safe to call repeatedly.
		/// </summary>
		public static void EnsureAttachedToAllEventSystems()
		{
			var systems = Object.FindObjectsOfType<EventSystem>();
			for (int i = 0; i < systems.Length; i++)
			{
				AttachTo(systems[i]);
			}
		}

		private static void AttachTo(EventSystem eventSystem)
		{
			if (eventSystem == null) return;

			var ours = eventSystem.GetComponent<MapboxBaseInput>();
			if (ours == null) ours = eventSystem.gameObject.AddComponent<MapboxBaseInput>();

			// BaseInputModule.input explicitly skips subclasses when picking a
			// default (it uses GetType() == typeof(BaseInput)), so AddComponent
			// alone isn't enough — we have to assign inputOverride directly.
			// Respect any user-provided override (don't clobber it).
			var modules = eventSystem.GetComponents<BaseInputModule>();
			for (int i = 0; i < modules.Length; i++)
			{
				if (modules[i].inputOverride == null) modules[i].inputOverride = ours;
			}
		}

		// Named Pointer (not Input) to avoid shadowing UnityEngine.Input inside
		// this class — UnityEngine.Input.* references appear in inherited
		// base-class XML comments and IDE auto-complete would mis-resolve.
		private IPointerInput _pointer;
		private IPointerInput Pointer => _pointer ?? (_pointer = PointerInputFactory.Create());

		public override Vector2 mousePosition => Pointer.MousePosition;
		public override bool mousePresent => true;

		public override bool GetMouseButton(int button)
		{
			if (button == 0) return Pointer.MouseLeftHeld;
			if (button == 1) return Pointer.MouseRightHeld;
			return false;
		}

		public override bool GetMouseButtonDown(int button)
		{
			if (button == 0) return Pointer.MouseLeftPressedThisFrame;
			if (button == 1) return Pointer.MouseRightPressedThisFrame;
			return false;
		}

		// IPointerInput doesn't expose mouse-up — UGUI press handling is driven
		// by Down + held state; Up is mainly used for drag-release which our
		// demo scenes don't rely on.
		public override bool GetMouseButtonUp(int button) => false;

		// Pointer.MouseScrollY is normalized to legacy-axis scale (~0.1 per
		// notch). UGUI ScrollRect feeds this directly into scrollSensitivity,
		// so the legacy scale matches what users get under "Old" mode today.
		public override Vector2 mouseScrollDelta => new Vector2(0f, Pointer.MouseScrollY);

		public override int touchCount => Pointer.TouchCount;
		public override bool touchSupported => true;

		public override Touch GetTouch(int index)
		{
			var phase = TouchPhase.Stationary;
			switch (Pointer.GetTouchPhase(index))
			{
				case PointerTouchPhase.Began: phase = TouchPhase.Began; break;
				case PointerTouchPhase.Moved: phase = TouchPhase.Moved; break;
				case PointerTouchPhase.Stationary: phase = TouchPhase.Stationary; break;
				case PointerTouchPhase.Ended: phase = TouchPhase.Ended; break;
				case PointerTouchPhase.Canceled: phase = TouchPhase.Canceled; break;
			}

			var position = Pointer.GetTouchPosition(index);
			return new Touch
			{
				fingerId = Pointer.GetTouchId(index),
				position = position,
				rawPosition = position,
				deltaPosition = new Vector2(0f, Pointer.GetTouchDeltaY(index)),
				phase = phase,
			};
		}

		// Submit / Cancel / keyboard nav axes — not used by our map demo UIs.
		// Returning safe defaults prevents BaseInput's defaults from calling
		// UnityEngine.Input.GetAxisRaw / GetButtonDown (throws under New-only).
		// If a demo grows real keyboard UI navigation, add the equivalents to
		// IPointerInput (or a new IKeyboardNavInput) and wire them here.
		public override float GetAxisRaw(string axisName) => 0f;
		public override bool GetButtonDown(string buttonName) => false;

		// IME (CJK input composition) — return inert defaults. Map demos don't
		// have text-input UI; leaving the base impl would route to Input.* and
		// throw under New-only. Setters silently ignore — refuse to mutate
		// UnityEngine.Input from this shim.
		public override string compositionString => string.Empty;

		public override IMECompositionMode imeCompositionMode
		{
			get => IMECompositionMode.Auto;
			set { /* intentional no-op */ }
		}

		public override Vector2 compositionCursorPos
		{
			get => Vector2.zero;
			set { /* intentional no-op */ }
		}
	}
}
