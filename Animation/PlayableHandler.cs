using NaughtyAttributes;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UC
{
    [RequireComponent(typeof(Animator))]
    public class PlayableHandler : MonoBehaviour
    {
        [Header("Startup")]
        [SerializeField]
        private bool            playOnStart = true;
        [SerializeField, ShowIf(nameof(playOnStart))]
        private AnimationClip   startAnimation;
        [SerializeField, Min(0.0f)]
        private float           animationSpeed = 1.0f;
        [SerializeField]
        private bool            removeAnimatorController = true;

        private Animator                animator;
        private PlayableGraph           graph;
        private AnimationMixerPlayable  mixer;

        private readonly AnimationClipPlayable[] clipPlayables = new AnimationClipPlayable[2];

        private int         activeInput;
        private Coroutine   transitionCoroutine;

        private float           transitionElapsed;
        private float           transitionDuration;
        private AnimationClip   transitionFromClip;
        private AnimationClip   transitionToClip;

        private bool    fauxLoopEnabled;
        private float   fauxLoopStartNormalized;
        private float   fauxLoopEndNormalized = 1f;
        private int     fauxLoopMaxLoops = -1;
        private int     fauxLoopCompletedLoops;

        public AnimationClip currentClip { get; private set; }

        public bool IsTransitioning => transitionCoroutine != null;

        public AnimationClip TransitionFromClip => transitionFromClip;
        public AnimationClip TransitionToClip => transitionToClip;
        public float TransitionElapsed => transitionElapsed;
        public float TransitionDuration => transitionDuration;

        public float TransitionNormalizedTime
        {
            get
            {
                if ((!IsTransitioning) || (transitionDuration <= 0f))
                    return 0f;

                return Mathf.Clamp01(transitionElapsed / transitionDuration);
            }
        }

        public bool FauxLoopEnabled => fauxLoopEnabled;
        public float FauxLoopStartNormalized => fauxLoopStartNormalized;
        public float FauxLoopEndNormalized => fauxLoopEndNormalized;
        public int FauxLoopMaxLoops => fauxLoopMaxLoops;
        public int FauxLoopCompletedLoops => fauxLoopCompletedLoops;

        public double CurrentTime
        {
            get
            {
                AnimationClipPlayable playable = GetActiveClipPlayable();

                if (!playable.IsValid())
                    return 0.0;

                return playable.GetTime();
            }
        }

        public float CurrentNormalizedTime
        {
            get
            {
                if ((currentClip == null) || (currentClip.length <= 0f))
                    return 0f;

                float t = (float)CurrentTime / currentClip.length;

                // For display, use wrapped normalized time.
                return Mathf.Repeat(t, 1f);
            }
        }

        public float CurrentRawNormalizedTime
        {
            get
            {
                if ((currentClip == null) || (currentClip.length <= 0f))
                    return 0f;

                return (float)(CurrentTime / currentClip.length);
            }
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();

            if (removeAnimatorController)
                animator.runtimeAnimatorController = null;

            BuildGraph();
        }

        private void Start()
        {
            if ((playOnStart) && (startAnimation != null))
                Play(startAnimation);
        }

        private void Update()
        {
            if (!graph.IsValid())
                return;

            graph.Evaluate(Time.deltaTime * animationSpeed);

            UpdateFauxLoop();
        }

        private void BuildGraph()
        {
            if (graph.IsValid())
                return;

            graph = PlayableGraph.Create($"{name}_PlayableHandler");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            mixer = AnimationMixerPlayable.Create(graph, 2);

            var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            output.SetSourcePlayable(mixer);

            mixer.SetInputWeight(0, 0f);
            mixer.SetInputWeight(1, 0f);

            graph.Play();
        }

        public void Play(AnimationClip clip)
        {
            if (clip == null)
                return;

            EnsureGraph();

            StopTransition();

            ClearInput(0);
            ClearInput(1);

            activeInput = 0;

            var playable = CreateClipPlayable(clip);
            clipPlayables[activeInput] = playable;

            graph.Connect(playable, 0, mixer, activeInput);

            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);

            currentClip = clip;
            fauxLoopCompletedLoops = 0;
        }

        public void CrossFade(AnimationClip clip, float duration)
        {
            if (clip == null)
                return;

            EnsureGraph();

            if (duration <= 0f || currentClip == null)
            {
                Play(clip);
                return;
            }

            StopTransition();
            NormalizeToStrongestInput();

            transitionCoroutine = StartCoroutine(CrossFadeRoutine(clip, duration));
        }

        public void CrossFade(AnimationClip fromClip, AnimationClip toClip, float duration)
        {
            Play(fromClip);
            CrossFade(toClip, duration);
        }

        public void Stop()
        {
            StopTransition();

            ClearInput(0);
            ClearInput(1);

            mixer.SetInputWeight(0, 0f);
            mixer.SetInputWeight(1, 0f);

            currentClip = null;
            DisableFauxLoop();
        }

        public void EnableFauxLoop(float startNormalizedTime, float endNormalizedTime, int loopCount = -1, bool jumpToStart = false)
        {
            fauxLoopStartNormalized = Mathf.Clamp01(startNormalizedTime);
            fauxLoopEndNormalized = Mathf.Clamp01(endNormalizedTime);

            if (fauxLoopEndNormalized <= fauxLoopStartNormalized)
            {
                fauxLoopEndNormalized = Mathf.Min(1f, fauxLoopStartNormalized + 0.001f);
            }

            fauxLoopMaxLoops = loopCount;
            fauxLoopCompletedLoops = 0;
            fauxLoopEnabled = true;

            if ((jumpToStart) && (currentClip != null))
            {
                AnimationClipPlayable playable = GetActiveClipPlayable();

                if (playable.IsValid())
                {
                    playable.SetTime(currentClip.length * fauxLoopStartNormalized);

                    // Force immediate resample after the time jump.
                    if (graph.IsValid())
                        graph.Evaluate(0f);
                }
            }
        }

        public void DisableFauxLoop()
        {
            fauxLoopEnabled = false;
            fauxLoopStartNormalized = 0f;
            fauxLoopEndNormalized = 1f;
            fauxLoopMaxLoops = -1;
            fauxLoopCompletedLoops = 0;
        }

        private void UpdateFauxLoop()
        {
            if (!fauxLoopEnabled)
                return;

            if (IsTransitioning)
                return;

            if ((currentClip == null) || (currentClip.length <= 0f))
                return;

            AnimationClipPlayable playable = GetActiveClipPlayable();

            if (!playable.IsValid())
                return;

            double startTime = currentClip.length * fauxLoopStartNormalized;
            double endTime = currentClip.length * fauxLoopEndNormalized;

            if (endTime <= startTime)
                return;

            double time = playable.GetTime();

            if (time < startTime)
            {
                playable.SetTime(startTime);
                graph.Evaluate(0f);
                return;
            }

            if (time < endTime)
                return;

            fauxLoopCompletedLoops++;

            if ((fauxLoopMaxLoops >= 0) && (fauxLoopCompletedLoops >= fauxLoopMaxLoops))
            {
                fauxLoopEnabled = false;

                // Leave the animation at the loop end, then allow normal playback from there.
                playable.SetTime(endTime);
                graph.Evaluate(0f);

                return;
            }

            double overflow = time - endTime;
            double loopLength = endTime - startTime;

            // Protect against very large delta times.
            if (loopLength > 0.0)
                overflow %= loopLength;

            playable.SetTime(startTime + overflow);
            graph.Evaluate(0f);
        }

        private IEnumerator CrossFadeRoutine(AnimationClip clip, float duration)
        {
            int fromInput = activeInput;
            int toInput = 1 - activeInput;

            ClearInput(toInput);

            var playable = CreateClipPlayable(clip);
            clipPlayables[toInput] = playable;

            graph.Connect(playable, 0, mixer, toInput);

            float fromStartWeight = mixer.GetInputWeight(fromInput);

            mixer.SetInputWeight(toInput, 0f);

            transitionFromClip = currentClip;
            transitionToClip = clip;
            transitionElapsed = 0f;
            transitionDuration = duration;

            while (transitionElapsed < transitionDuration)
            {
                transitionElapsed += Time.deltaTime;

                float t = Mathf.Clamp01(transitionElapsed / transitionDuration);

                mixer.SetInputWeight(fromInput, Mathf.Lerp(fromStartWeight, 0f, t));
                mixer.SetInputWeight(toInput, t);

                yield return null;
            }

            mixer.SetInputWeight(fromInput, 0f);
            mixer.SetInputWeight(toInput, 1f);

            ClearInput(fromInput);

            activeInput = toInput;
            currentClip = clip;

            transitionCoroutine = null;
            transitionElapsed = 0f;
            transitionDuration = 0f;
            transitionFromClip = null;
            transitionToClip = null;

            fauxLoopCompletedLoops = 0;
        }

        private AnimationClipPlayable CreateClipPlayable(AnimationClip clip)
        {
            var playable = AnimationClipPlayable.Create(graph, clip);

            playable.SetTime(0.0);
            playable.SetSpeed(1.0);

            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);

            return playable;
        }

        private AnimationClipPlayable GetActiveClipPlayable()
        {
            if ((activeInput < 0) || (activeInput >= clipPlayables.Length))
                return default;

            return clipPlayables[activeInput];
        }

        private void ClearInput(int inputIndex)
        {
            if ((!graph.IsValid()) || (!mixer.IsValid()))
                return;

            var input = mixer.GetInput(inputIndex);

            if (!input.IsValid())
            {
                mixer.SetInputWeight(inputIndex, 0f);
                clipPlayables[inputIndex] = default;
                return;
            }

            graph.Disconnect(mixer, inputIndex);
            graph.DestroySubgraph(input);

            mixer.SetInputWeight(inputIndex, 0f);
            clipPlayables[inputIndex] = default;
        }

        private void StopTransition()
        {
            if (transitionCoroutine == null)
                return;

            StopCoroutine(transitionCoroutine);

            transitionCoroutine = null;
            transitionElapsed = 0f;
            transitionDuration = 0f;
            transitionFromClip = null;
            transitionToClip = null;
        }

        private void NormalizeToStrongestInput()
        {
            float w0 = mixer.GetInputWeight(0);
            float w1 = mixer.GetInputWeight(1);

            activeInput = (w1 > w0) ? (1) : (0);
            int inactiveInput = 1 - activeInput;

            mixer.SetInputWeight(activeInput, 1f);
            mixer.SetInputWeight(inactiveInput, 0f);

            ClearInput(inactiveInput);
        }

        private void EnsureGraph()
        {
            if (!graph.IsValid())
                BuildGraph();
        }

        private void OnDestroy()
        {
            StopTransition();

            if (graph.IsValid())
                graph.Destroy();
        }
    }
}