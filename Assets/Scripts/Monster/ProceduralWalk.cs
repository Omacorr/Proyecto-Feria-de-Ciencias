using UnityEngine;

/// <summary>
/// Animación procedural de caminata/carrera para el monstruo riggeado con
/// <c>MonsterAutoRig</c> (prefab Monstruo_Rig). NO usa Animator: mide la
/// velocidad horizontal del propio transform (lo mueva MonsterChaser,
/// MonsterController o quien sea) y mueve los huesos en LateUpdate encima de
/// su pose de reposo.
///
/// Convenciones: el objeto con este script es la RAIZ del monstruo, mira a +Z
/// y tiene Y hacia arriba. Los ejes de cada hueso se calculan en Awake a
/// partir de la pose de reposo (dirección del hueso x "adelante"), así que
/// funciona aunque los brazos estén abiertos en T: el swing siempre es hacia
/// adelante/atrás del cuerpo. Los brazos se bajan primero con
/// <c>Arm Droop Degrees</c> (el modelo original tiene los brazos abiertos).
/// </summary>
[DisallowMultipleComponent]
public class ProceduralWalk : MonoBehaviour
{
    [Header("Huesos (si están vacíos se buscan por nombre)")]
    [SerializeField] private Transform _hips;
    [SerializeField] private Transform _spine;
    [SerializeField] private Transform _chest;
    [SerializeField] private Transform _neck;
    [SerializeField] private Transform _head;
    [SerializeField] private Transform _leftUpperArm;
    [SerializeField] private Transform _leftLowerArm;
    [SerializeField] private Transform _rightUpperArm;
    [SerializeField] private Transform _rightLowerArm;
    [SerializeField] private Transform _leftThigh;
    [SerializeField] private Transform _leftShin;
    [SerializeField] private Transform _rightThigh;
    [SerializeField] private Transform _rightShin;
    [SerializeField] private Transform _leftFoot;
    [SerializeField] private Transform _rightFoot;

    [Header("Velocidad")]
    [Tooltip("Velocidad (m/s) a la que el swing llega al 100 %. Por debajo, se atenúa hasta quedarse quieto.")]
    [SerializeField] private float _speedForFullSwing = 1.0f;
    [Tooltip("Suavizado de la velocidad medida (más alto = reacciona más rápido).")]
    [SerializeField] private float _speedSmoothing = 8f;
    [Tooltip("Saltos de posición más rápidos que esto (m/s) se consideran teletransportes y se ignoran.")]
    [SerializeField] private float _maxPlausibleSpeed = 25f;
    [Tooltip("Velocidad desde la que empieza a 'correr' (más amplitud y más inclinación).")]
    [SerializeField] private float _runStartSpeed = 2.5f;
    [Tooltip("Velocidad a la que ya es carrera completa.")]
    [SerializeField] private float _runFullSpeed = 5f;
    [Tooltip("Multiplicador de amplitud en carrera completa.")]
    [SerializeField] private float _runAmplitudeMultiplier = 1.5f;

    [Header("Zancada")]
    [Tooltip("Ciclos de paso por segundo = base + por m/s * velocidad.")]
    [SerializeField] private float _baseStrideFrequency = 0.35f;
    [SerializeField] private float _strideFrequencyPerMps = 0.5f;
    [SerializeField] private float _maxStrideFrequency = 3f;
    [SerializeField] private float _thighSwingDegrees = 28f;
    [SerializeField] private float _kneeBendDegrees = 45f;
    [Tooltip("Flexión constante de rodilla al caminar (grados).")]
    [SerializeField] private float _kneeRestBend = 6f;
    [Tooltip("Cuánto del ángulo de pierna se compensa en el pie para que quede plano (0..1).")]
    [SerializeField] private float _footLevel = 0.7f;

    [Header("Brazos")]
    [Tooltip("Cuánto se bajan los brazos desde su pose de reposo (el modelo los tiene abiertos). 0 = no tocar.")]
    [SerializeField] private float _armDroopDegrees = 45f;
    [SerializeField] private float _armSwingDegrees = 25f;
    [Tooltip("Flexión base de los codos (grados, hacia adelante).")]
    [SerializeField] private float _elbowRestBend = 15f;
    [SerializeField] private float _elbowSwingBend = 25f;
    [Tooltip("Retraso del codo respecto al hombro (radianes del ciclo).")]
    [SerializeField] private float _elbowLagRadians = 0.8f;

    [Header("Torso")]
    [Tooltip("Inclinación hacia adelante al caminar (joroba tétrica, 15-25 grados).")]
    [SerializeField] private float _leanDegrees = 20f;
    [Tooltip("Fracción de la inclinación que se mantiene quieto.")]
    [SerializeField, Range(0f, 1f)] private float _leanIdleFraction = 0.7f;
    [Tooltip("Inclinación extra al correr.")]
    [SerializeField] private float _runLeanExtra = 12f;
    [SerializeField] private float _hipsBobMeters = 0.04f;
    [SerializeField] private float _hipsRollDegrees = 3f;
    [SerializeField] private float _hipsYawDegrees = 6f;

    [Header("Idle")]
    [SerializeField] private float _breathHz = 0.25f;
    [SerializeField] private float _breathDegrees = 1.5f;
    [Tooltip("Rango de segundos entre espasmos de cabeza.")]
    [SerializeField] private Vector2 _twitchInterval = new Vector2(1.5f, 4f);
    [Tooltip("Rango de amplitud (grados) de cada espasmo.")]
    [SerializeField] private Vector2 _twitchDegrees = new Vector2(20f, 35f);
    [SerializeField] private Vector2 _twitchHold = new Vector2(0.15f, 0.45f);

    [Header("Andar pesado")]
    [Tooltip("Escala TODO el andar pesado (0 = comportamiento anterior exacto, 1 = completo).")]
    [SerializeField, Range(0f, 1f)] private float _heaviness = 1f;
    [Tooltip("Multiplicador de la frecuencia de zancada (menor = pasos mas lentos).")]
    [SerializeField] private float _strideFrequencyMultiplier = 0.75f;
    [Tooltip("Cuanto se frena la fase alrededor de cada pisada (apoyo largo) y se acelera en el vuelo (0..0.9).")]
    [SerializeField, Range(0f, 0.9f)] private float _phaseBrake = 0.45f;
    [Tooltip("Cuanto mas dura la pisada de la pierna mala (derecha), 0..0.5.")]
    [SerializeField, Range(0f, 0.5f)] private float _limpTiming = 0.15f;
    [Tooltip("Metros que baja la cadera de golpe en cada pisada (se recupera mas lento).")]
    [SerializeField] private float _stepDropMeters = 0.06f;
    [Tooltip("Grados extra de flexion de Spine/Chest hacia adelante en el impacto.")]
    [SerializeField] private float _impactTorsoDegrees = 4f;
    [Tooltip("Grados de cabeceo que el impacto le da a la cabeza (resorte con retraso).")]
    [SerializeField] private float _headNodDegrees = 9f;
    [Tooltip("Frecuencia (Hz) del resorte de la cabeza.")]
    [SerializeField] private float _headSpringHz = 3.2f;
    [Tooltip("Amortiguacion del resorte de la cabeza (0.2 rebota, 1 sin rebote).")]
    [SerializeField] private float _headSpringDamping = 0.3f;
    [Tooltip("Flexion extra de rodillas al caminar (agazapado), grados.")]
    [SerializeField] private float _crouchKneeDegrees = 14f;
    [Tooltip("Roll extra de cadera hacia la pierna de apoyo (grados); el pecho hace contra-roll.")]
    [SerializeField] private float _weightShiftRollDegrees = 4f;
    [Tooltip("Desplazamiento lateral de la cadera sobre la pierna de apoyo (metros).")]
    [SerializeField] private float _hipShiftMeters = 0.03f;
    [Tooltip("Cuanto menos levanta la rodilla la pierna derecha en el vuelo (0..1).")]
    [SerializeField, Range(0f, 1f)] private float _limpKneeReduction = 0.45f;
    [Tooltip("Cuanto menos swing de muslo tiene la pierna derecha (0..1).")]
    [SerializeField, Range(0f, 1f)] private float _limpSwingReduction = 0.3f;
    [Tooltip("Pie derecho caido / arrastrado (grados).")]
    [SerializeField] private float _limpFootDrop = 18f;
    [Tooltip("Cuanto se reduce el swing activo de los brazos (0..1).")]
    [SerializeField, Range(0f, 1f)] private float _armSwingReduction = 0.65f;
    [Tooltip("Multiplicador de la flexion de codos (menor = mas rectos).")]
    [SerializeField, Range(0f, 1f)] private float _elbowStraighten = 0.3f;
    [Tooltip("Frecuencia (Hz) del pendulo de cada brazo.")]
    [SerializeField] private float _armSpringHz = 1.6f;
    [Tooltip("Amortiguacion del pendulo de los brazos (menor = oscila mas).")]
    [SerializeField] private float _armSpringDamping = 0.25f;
    [Tooltip("Cuanto empuja el yaw del torso a los brazos (grados por grado).")]
    [SerializeField] private float _armYawGain = 1.4f;
    [Tooltip("Cuanto empuja el roll del torso a los brazos (grados por grado).")]
    [SerializeField] private float _armRollGain = 1.0f;
    [Tooltip("Grados que el impacto de cada pisada le da a los brazos.")]
    [SerializeField] private float _armImpactDegrees = 10f;
    [Tooltip("Respiracion al caminar: frecuencia (Hz).")]
    [SerializeField] private float _heavyBreathHz = 0.6f;
    [Tooltip("Respiracion al caminar: amplitud en Chest (grados).")]
    [SerializeField] private float _heavyBreathDegrees = 3.5f;
    [Tooltip("Fraccion de la respiracion pesada que tambien mueve el cuello.")]
    [SerializeField] private float _neckBreathShare = 0.25f;
    [Tooltip("Cabeza colgando: pitch extra hacia abajo al caminar (grados).")]
    [SerializeField] private float _headHangDegrees = 14f;
    [Tooltip("Multiplicador del intervalo entre espasmos mientras camina (mas alto = menos frecuentes).")]
    [SerializeField] private float _twitchWalkIntervalMultiplier = 2.2f;

    private class Joint
    {
        public Transform t;
        public Quaternion rest;
        public Vector3 restPos;
        public Vector3 sw, roll, yaw, up, lat;
    }

    private Joint _jHips, _jSpine, _jChest, _jNeck, _jHead;
    private Joint _jLUA, _jLLA, _jRUA, _jRLA, _jLT, _jLS, _jRT, _jRS, _jLF, _jRF;

    private float _speedOverride = -1f;
    private float _speed;
    private float _phase;
    private Vector3 _lastPos;
    private float _breathPhase;
    private float _headAng, _headVel;
    private float _armAngL, _armVelL, _armAngR, _armVelR;
    private float _twitchWalkScale = 1f;
    private bool _hasLast;

    private float _nextTwitch;
    private float _twitchEnd;
    private bool _twitching;
    private Vector2 _twitchTarget;
    private Vector2 _twitchCur;

    /// <summary>Fuerza una velocidad (m/s) en vez de medirla. Negativo = volver a medir.</summary>
    public void SetSpeedOverride(float speed) { _speedOverride = speed; }

    /// <summary>Vuelve a medir la velocidad del transform.</summary>
    public void ClearSpeedOverride() { _speedOverride = -1f; }

    [ContextMenu("Buscar huesos por nombre")]
    public void AutoFindBones()
    {
        Find(ref _hips, "Hips");
        Find(ref _spine, "Spine");
        Find(ref _chest, "Chest");
        Find(ref _neck, "Neck");
        Find(ref _head, "Head");
        Find(ref _leftUpperArm, "UpperArm_L");
        Find(ref _leftLowerArm, "LowerArm_L");
        Find(ref _rightUpperArm, "UpperArm_R");
        Find(ref _rightLowerArm, "LowerArm_R");
        Find(ref _leftThigh, "Thigh_L");
        Find(ref _leftShin, "Shin_L");
        Find(ref _rightThigh, "Thigh_R");
        Find(ref _rightShin, "Shin_R");
        Find(ref _leftFoot, "Foot_L");
        Find(ref _rightFoot, "Foot_R");
    }

    private void Find(ref Transform field, string boneName)
    {
        if (field != null) return;
        field = FindDeep(transform, boneName);
    }

    private static Transform FindDeep(Transform root, string boneName)
    {
        foreach (Transform c in root)
        {
            if (c.name == boneName) return c;
            Transform r = FindDeep(c, boneName);
            if (r != null) return r;
        }
        return null;
    }

    private void Awake()
    {
        AutoFindBones();
        // Primero bajar los brazos, así los ejes y reposos se capturan ya con esa pose.
        Droop(_leftUpperArm);
        Droop(_rightUpperArm);

        _jHips = Make(_hips, Vector3.up, false);
        _jSpine = Make(_spine, Vector3.up, false);
        _jChest = Make(_chest, Vector3.up, false);
        _jNeck = Make(_neck, Vector3.up, false);
        _jHead = Make(_head, Vector3.up, false);
        _jLUA = Make(_leftUpperArm, Vector3.left, true);
        _jLLA = Make(_leftLowerArm, Vector3.left, true);
        _jRUA = Make(_rightUpperArm, Vector3.right, true);
        _jRLA = Make(_rightLowerArm, Vector3.right, true);
        _jLT = Make(_leftThigh, Vector3.down, true);
        _jLS = Make(_leftShin, Vector3.down, true);
        _jRT = Make(_rightThigh, Vector3.down, true);
        _jRS = Make(_rightShin, Vector3.down, true);
        _jLF = Make(_leftFoot, Vector3.down, false);
        _jRF = Make(_rightFoot, Vector3.down, false);

        _nextTwitch = Time.time + Random.Range(_twitchInterval.x, _twitchInterval.y);
        _phase = Random.value * Mathf.PI * 2f;
    }

    private void OnEnable()
    {
        _hasLast = false;
        _headAng = _headVel = 0f;
        _armAngL = _armVelL = _armAngR = _armVelR = 0f;
        _breathPhase = Time.time * Mathf.PI * 2f * _breathHz;
        _twitchWalkScale = 1f;
    }

    /// <summary>Resorte de segundo orden, integracion semi-implicita (estable con dt acotado).</summary>
    private static void SpringStep(ref float x, ref float v, float target, float hz, float damping, float dt)
    {
        float w = Mathf.PI * 2f * hz;
        float a = w * w * (target - x) - 2f * damping * w * v;
        v += a * dt;
        x += v * dt;
    }

    private Vector3 LocalAxis(Transform t, Vector3 rootAxis)
    {
        Vector3 w = transform.TransformDirection(rootAxis);
        Vector3 l = t.parent != null ? t.parent.InverseTransformDirection(w) : w;
        return l.normalized;
    }

    private void Droop(Transform upper)
    {
        if (upper == null || _armDroopDegrees <= 0.01f || upper.childCount == 0) return;
        Vector3 d = transform.InverseTransformDirection(upper.GetChild(0).position - upper.position).normalized;
        float angle = Mathf.Min(_armDroopDegrees, Vector3.Angle(d, Vector3.down));
        Vector3 axis = Vector3.Cross(d, Vector3.down);
        if (axis.sqrMagnitude < 1e-6f || angle < 0.1f) return;
        upper.localRotation = Quaternion.AngleAxis(angle, LocalAxis(upper, axis.normalized)) * upper.localRotation;
    }

    private Joint Make(Transform t, Vector3 hintDir, bool useChild)
    {
        if (t == null) return null;
        Vector3 d = hintDir;
        if (useChild && t.childCount > 0)
        {
            Vector3 c = transform.InverseTransformDirection(t.GetChild(0).position - t.position);
            if (c.sqrMagnitude > 1e-8f) d = c.normalized;
        }
        else if (hintDir.x != 0f && useChild)
        {
            d = new Vector3(Mathf.Sign(transform.InverseTransformPoint(t.position).x), 0, 0);
        }

        Vector3 sw = Vector3.Cross(d, Vector3.forward);
        if (sw.sqrMagnitude < 1e-4f) sw = Vector3.right;
        var j = new Joint
        {
            t = t,
            rest = t.localRotation,
            restPos = t.localPosition,
            sw = LocalAxis(t, sw.normalized),
            roll = LocalAxis(t, Vector3.forward),
            yaw = LocalAxis(t, Vector3.up),
            up = LocalAxis(t, Vector3.up),
            lat = LocalAxis(t, Vector3.right)
        };
        return j;
    }

    private static void Pose(Joint j, float swing, float roll = 0f, float yaw = 0f)
    {
        if (j == null) return;
        j.t.localRotation = Quaternion.AngleAxis(swing, j.sw)
                          * Quaternion.AngleAxis(roll, j.roll)
                          * Quaternion.AngleAxis(yaw, j.yaw)
                          * j.rest;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // --- velocidad horizontal ---
        float raw = 0f;
        Vector3 p = transform.position;
        if (_hasLast)
        {
            Vector3 delta = p - _lastPos;
            delta.y = 0f;
            raw = delta.magnitude / dt;
            if (raw > _maxPlausibleSpeed) raw = 0f; // teletransporte
        }
        _lastPos = p;
        _hasLast = true;
        float target = _speedOverride >= 0f ? _speedOverride : raw;
        _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-_speedSmoothing * dt));

        float move = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_speed / Mathf.Max(0.01f, _speedForFullSwing)));
        float run = Mathf.InverseLerp(_runStartSpeed, Mathf.Max(_runStartSpeed + 0.01f, _runFullSpeed), _speed);
        float amp = move * Mathf.Lerp(1f, _runAmplitudeMultiplier, run);

        float h = Mathf.Clamp01(_heaviness);
        float hm = h * move;
        float sdt = Mathf.Min(dt, 0.05f);

        float freq = Mathf.Min(_maxStrideFrequency, _baseStrideFrequency + _strideFrequencyPerMps * _speed)
                   * Mathf.Lerp(1f, _strideFrequencyMultiplier, h);
        // Velocidad de fase modulada: lenta cerca de cada pisada (fase = pi/2 + n*pi) y rapida
        // en el vuelo; la pisada derecha (cos > 0) dura un poco mas. Siempre > 0 => monotona.
        float rate = 1f + _phaseBrake * h * Mathf.Cos(2f * _phase) - _limpTiming * h * Mathf.Cos(_phase);
        rate = Mathf.Max(0.25f, rate);
        _phase += Mathf.PI * 2f * freq * dt * move * rate;
        _phase %= Mathf.PI * 2f; // evita pérdida de precisión
        float s = Mathf.Sin(_phase);
        float c = Mathf.Cos(_phase);

        // Impacto: u = fraccion transcurrida desde la ultima pisada. Ataque rapido, recuperacion lenta.
        float q = (_phase - Mathf.PI * 0.5f) / Mathf.PI;
        float u = q - Mathf.Floor(q);
        float omu = 1f - u;
        float pulse = Mathf.Clamp01((1f - Mathf.Exp(-30f * u)) * omu * omu * omu / 0.7f);

        // --- piernas ---
        float rScale = Mathf.Lerp(1f, 1f - _limpSwingReduction, h);
        float rKnee = Mathf.Lerp(1f, 1f - _limpKneeReduction, h);
        float crouch = _crouchKneeDegrees * hm;
        float flightL = Mathf.Max(0f, c);
        float flightR = Mathf.Max(0f, -c);
        float thL = s * _thighSwingDegrees * amp;
        float thR = -thL * rScale;
        float kneeL = -(_kneeRestBend * move + crouch + _kneeBendDegrees * amp * flightL);
        float kneeR = -(_kneeRestBend * move + crouch + _kneeBendDegrees * amp * flightR * rKnee);
        float footDropR = _limpFootDrop * hm * (0.5f + 0.5f * flightR);
        Pose(_jLT, thL);
        Pose(_jRT, thR);
        Pose(_jLS, kneeL);
        Pose(_jRS, kneeR);
        Pose(_jLF, -(thL + kneeL) * _footLevel);
        Pose(_jRF, -(thR + kneeR) * _footLevel - footDropR);

        // --- valores del torso (los usan tambien los brazos) ---
        float yaw = s * _hipsYawDegrees * amp;
        float rollShift = c * _weightShiftRollDegrees * hm; // hacia la pierna de apoyo
        float impactFlex = _impactTorsoDegrees * pulse * hm;

        // --- brazos: swing directo (h=0) mezclado con pendulo de resorte (h=1) ---
        float armDirL = -s * _armSwingDegrees * amp;
        float armDirR = -armDirL;
        float armActive = Mathf.Lerp(1f, 1f - _armSwingReduction, h);
        float kick = pulse * _armImpactDegrees * hm;
        float tgtL = armDirL * armActive - yaw * _armYawGain * h - rollShift * _armRollGain + kick;
        float tgtR = armDirR * armActive + yaw * _armYawGain * h + rollShift * _armRollGain + kick;
        SpringStep(ref _armAngL, ref _armVelL, tgtL, _armSpringHz, _armSpringDamping, sdt);
        SpringStep(ref _armAngR, ref _armVelR, tgtR, _armSpringHz * 1.12f, _armSpringDamping, sdt);
        float armL = Mathf.Lerp(armDirL, _armAngL, h);
        float armR = Mathf.Lerp(armDirR, _armAngR, h);

        float elScale = Mathf.Lerp(1f, _elbowStraighten, h);
        float phL = _phase + Mathf.PI;
        float phR = _phase;
        float elL = elScale * (_elbowRestBend + _elbowSwingBend * amp * (0.5f + 0.5f * Mathf.Sin(phL - _elbowLagRadians)));
        float elR = elScale * (_elbowRestBend + _elbowSwingBend * amp * (0.5f + 0.5f * Mathf.Sin(phR - _elbowLagRadians)));
        Pose(_jLUA, armL);
        Pose(_jRUA, armR);
        Pose(_jLLA, elL);
        Pose(_jRLA, elR);

        // --- respiracion (mas rapida y amplia al caminar, fase continua) ---
        _breathPhase += Mathf.PI * 2f * Mathf.Lerp(_breathHz, _heavyBreathHz, hm) * dt;
        _breathPhase %= Mathf.PI * 2f;
        float breath = Mathf.Sin(_breathPhase) * Mathf.Lerp(_breathDegrees, _heavyBreathDegrees, hm);
        float neckBreath = breath * _neckBreathShare * hm;

        // --- torso ---
        float lean = _leanDegrees * Mathf.Lerp(_leanIdleFraction, 1f, move) + _runLeanExtra * run;
        Pose(_jHips, 0f, s * _hipsRollDegrees * amp + rollShift, yaw);
        Pose(_jSpine, lean * 0.35f + impactFlex * 0.5f, -rollShift * 0.3f, -yaw * 0.8f);
        Pose(_jChest, lean * 0.40f + breath + impactFlex * 0.5f, -rollShift * 0.6f, -yaw * 0.5f);

        if (_jHips != null)
        {
            float bob = Mathf.Cos(_phase * 2f) * 0.5f * _hipsBobMeters * amp;
            float drop = _stepDropMeters * pulse * h * amp;
            float shift = c * _hipShiftMeters * hm;
            _jHips.t.localPosition = _jHips.restPos + _jHips.up * (bob - drop) + _jHips.lat * shift;
        }

        // --- cabeza: resorte con retraso + colgando + espasmos ---
        SpringStep(ref _headAng, ref _headVel, pulse * _headNodDegrees * hm, _headSpringHz, _headSpringDamping, sdt);
        float hang = _headHangDegrees * hm;
        _twitchWalkScale = Mathf.Lerp(1f, _twitchWalkIntervalMultiplier, hm);
        UpdateTwitch(dt);
        Pose(_jNeck, lean * 0.10f + _twitchCur.y * 0.3f + hang * 0.4f + _headAng * 0.4f + neckBreath, 0f, _twitchCur.x * 0.3f);
        Pose(_jHead, lean * 0.15f + _twitchCur.y * 0.7f + hang * 0.6f + _headAng * 0.6f, 0f, _twitchCur.x * 0.7f);
    }

    private void UpdateTwitch(float dt)
    {
        float now = Time.time;
        if (!_twitching && now >= _nextTwitch)
        {
            float sign = Random.value < 0.5f ? -1f : 1f;
            float mag = Random.Range(_twitchDegrees.x, _twitchDegrees.y);
            _twitchTarget = new Vector2(sign * mag, Random.Range(-0.5f, 0.5f) * mag);
            _twitchEnd = now + Random.Range(_twitchHold.x, _twitchHold.y);
            _twitching = true;
        }
        else if (_twitching && now >= _twitchEnd)
        {
            _twitchTarget = Vector2.zero;
            _twitching = false;
            _nextTwitch = now + Random.Range(_twitchInterval.x, _twitchInterval.y) * _twitchWalkScale;
        }
        float k = _twitching ? 35f : 6f; // salta rápido, vuelve despacio
        _twitchCur = Vector2.Lerp(_twitchCur, _twitchTarget, 1f - Mathf.Exp(-k * dt));
    }
}
