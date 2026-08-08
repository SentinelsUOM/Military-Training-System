'use client'
import { Canvas, useThree, useFrame } from '@react-three/fiber'
import { OrbitControls, Grid, Html, useGLTF } from '@react-three/drei'
import { useMemo, useState, useEffect, useRef, Suspense } from 'react'
import * as THREE from 'three'
import { SkeletonUtils } from 'three-stdlib'

// Phase 3 replay: interactive 3D with your real animated characters. Terrorists/trainee
// use the armed soldier GLB; hostages use the kneeling civilian GLB. Each actor plays the
// clip matching its recorded state (shoot/alert/walk/dead/idle), state-driven — not
// frame-exact to VR. Rooms are labelled and doors marked so you can place yourself.

const SOLDIER = '/models/soldier_anim.glb'   // clips: idle walk alert fire firewalk dead
const HOSTAGE = '/models/hostage.glb'        // clips: idle held
useGLTF.preload(SOLDIER)
useGLTF.preload(HOSTAGE)

const TRAINEE_COLOR = '#1f6feb'
const STATE_COLORS = {
  Engage: '#f85149', Panic: '#f85149', Alert: '#d29922', Fearful: '#d29922',
  Suspicious: '#58a6ff', TakeCover: '#bc8cff', Calm: '#3fb950', Follow: '#3fb950',
  Held: '#d29922', Down: '#6e7681', Idle: '#8b949e', Unknown: '#8b949e',
  // Indigo, not near-black: Freeze is tonic immobility, a hostage response research
  // ranks more severe than Panic (see HostageTab.jsx STATE_INFO) — it must not tint
  // as "calm/inactive" in the 3D replay.
  Freeze: '#818cf8', Neutralized: '#21262d',
}
const sane = (v) => Number.isFinite(v) && Math.abs(v) < 5000
// Module 1's "Corridor" RoomType is an interior hallway room in the generated
// BFS layout — a real, walled room, NOT the perimeter walkway outside the
// building (that's SceneBuilder's own invented corridor_seg_N geometry,
// rendered floor-only with no label by RoomWalls). Displaying both as
// "Corridor" reads as if we mislabeled a room, so this one shows as "Hallway"
// and "Corridor" is reserved for the walkway.
const prettyType = (t) => {
  const s = t || 'Room'
  if (s.toLowerCase() === 'corridor') return 'Hallway'
  return s.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function kindOf(actorId) {
  const id = (actorId || '').toLowerCase()
  if (id.startsWith('trainee')) return 'trainee'
  if (id.startsWith('hostage')) return 'hostage'
  return 'terrorist'
}
function colorFor(actorId, state) {
  if (kindOf(actorId) === 'trainee' || state === 'Trainee') return TRAINEE_COLOR
  if (kindOf(actorId) === 'hostage') return '#3fb950'
  return STATE_COLORS[state] || '#8b949e'
}
// State (+ movement) → clip. State wins over movement so alert/fire/dead actually show.
function clipFor(kind, state, moving) {
  if (kind === 'hostage') return state === 'Held' ? 'held' : 'idle'
  if (state === 'Down') return 'dead'
  if (state === 'Engage') return moving ? 'firewalk' : 'fire'
  if (state === 'Alert' || state === 'Suspicious' || state === 'Investigate') return 'alert'
  if (moving) return 'walk'
  return 'idle'
}

function poseAt(track, time) {
  if (!track.length) return null
  if (time <= track[0].t) return track[0]
  const last = track[track.length - 1]
  if (time >= last.t) return last
  for (let i = 0; i < track.length - 1; i++) {
    const a = track[i], b = track[i + 1]
    if (time >= a.t && time <= b.t) {
      const f = (time - a.t) / ((b.t - a.t) || 1)
      return { x: a.x + (b.x - a.x) * f, y: a.y + (b.y - a.y) * f, z: a.z + (b.z - a.z) * f, ry: a.ry, state: a.state }
    }
  }
  return last
}

function CapsuleBody({ color }) {
  return (
    <mesh position={[0, 0.9, 0]}>
      <capsuleGeometry args={[0.28, 1.0, 4, 12]} />
      <meshStandardMaterial color={color} />
    </mesh>
  )
}

function AnimatedActor({ url, clipName }) {
  const { scene, animations } = useGLTF(url)
  const cloned = useMemo(() => SkeletonUtils.clone(scene), [scene])
  const mixer = useMemo(() => new THREE.AnimationMixer(cloned), [cloned])
  const actions = useMemo(() => {
    const m = {}
    for (const clip of animations) {
      const a = mixer.clipAction(clip)
      const once = clip.name === 'dead'
      a.setLoop(once ? THREE.LoopOnce : THREE.LoopRepeat)
      a.clampWhenFinished = once
      m[clip.name] = a
    }
    return m
  }, [animations, mixer])
  const currentRef = useRef(null)

  useEffect(() => {
    const next = actions[clipName] || actions['idle']
    if (!next || next === currentRef.current) return
    next.reset().fadeIn(0.2).play()
    if (currentRef.current) currentRef.current.fadeOut(0.2)
    currentRef.current = next
  }, [clipName, actions])

  useFrame((_, delta) => mixer.update(delta))
  useEffect(() => () => mixer.stopAllAction(), [mixer])
  return <primitive object={cloned} />
}

function Actor({ actorId, pose, clip }) {
  if (!pose || !sane(pose.x) || !sane(pose.z)) return null
  const kind = kindOf(actorId)
  const url = kind === 'hostage' ? HOSTAGE : SOLDIER
  const color = colorFor(actorId, pose.state)
  const y = sane(pose.y) ? pose.y : 0
  return (
    <group position={[pose.x, y, pose.z]} rotation={[0, (pose.ry || 0) * Math.PI / 180, 0]}>
      <Suspense fallback={<CapsuleBody color={color} />}>
        <AnimatedActor url={url} clipName={clip} />
      </Suspense>
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.04, 0]}>
        <ringGeometry args={[0.34, 0.5, 28]} />
        <meshBasicMaterial color={color} transparent opacity={0.95} side={THREE.DoubleSide} />
      </mesh>
      <Html position={[0, 2.15, 0]} center distanceFactor={16} style={{ pointerEvents: 'none' }}>
        <div style={{ color, fontSize: 12, fontWeight: 700, whiteSpace: 'nowrap', textShadow: '0 0 4px #000, 0 0 4px #000' }}>
          {actorId}
        </div>
      </Html>
    </group>
  )
}

// A door: a bright translucent panel filling the wall opening, oriented by wallSide.
function Door({ door }) {
  if (!sane(door.x) || !sane(door.z)) return null
  const ns = door.wallSide === 'North' || door.wallSide === 'South'
  return (
    <group position={[door.x, 1.1, door.z]}>
      <mesh>
        <boxGeometry args={ns ? [1.4, 2.2, 0.12] : [0.12, 2.2, 1.4]} />
        <meshStandardMaterial color="#f0b429" emissive="#8a6100" transparent opacity={0.5} />
      </mesh>
    </group>
  )
}

function RoomWalls({ room, doors }) {
  const cx = room.centerX, cz = room.centerZ
  const w = sane(room.width) ? room.width : 0
  const d = sane(room.depth) ? room.depth : 0
  if (w <= 0 || d <= 0) return null
  const h = sane(room.height) && room.height > 0 ? room.height : 3
  const T = 0.15, GAP = 0.75
  const type = (room.type || '').toLowerCase()
  const floorColor = type.includes('corridor') ? '#2f4a63' : type.includes('hostage') ? '#2b2036' : type.includes('entry') ? '#1c2b22' : '#1b2230'

  // The perimeter corridor is captured as several merged floor rectangles
  // (SceneBuilder's own invented walkway geometry, id "corridor_seg_N" — see
  // Module4SessionController.CaptureLayout). Each is a real room-shaped RoomBox,
  // but they're segments of ONE continuous walkway, not separate walled rooms:
  // drawing full walls (with door-width cutouts only) on every segment's own
  // rectangle boundary puts a false partition wall wherever two segments happen
  // to touch, chopping the corridor into a maze of boxes instead of one loop.
  // Floor-only here so adjacent segments visually merge into a single path.
  const isCorridorSegment = typeof room.id === 'string' && room.id.startsWith('corridor_seg_')
  if (isCorridorSegment) {
    return (
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[cx, 0.01, cz]}>
        <planeGeometry args={[w, d]} />
        <meshStandardMaterial color={floorColor} transparent opacity={0.85} />
      </mesh>
    )
  }

  const segs = []
  for (const z of [cz - d / 2, cz + d / 2]) {
    const cuts = (doors || []).filter(dr => Math.abs(dr.z - z) < 0.7 && dr.x > cx - w / 2 - 0.1 && dr.x < cx + w / 2 + 0.1).map(dr => dr.x).sort((a, b) => a - b)
    let start = cx - w / 2
    for (const dx of cuts) { if (dx - GAP > start) segs.push({ x: (start + dx - GAP) / 2, z, len: dx - GAP - start, axis: 'x' }); start = Math.max(start, dx + GAP) }
    if (cx + w / 2 > start) segs.push({ x: (start + cx + w / 2) / 2, z, len: cx + w / 2 - start, axis: 'x' })
  }
  for (const x of [cx - w / 2, cx + w / 2]) {
    const cuts = (doors || []).filter(dr => Math.abs(dr.x - x) < 0.7 && dr.z > cz - d / 2 - 0.1 && dr.z < cz + d / 2 + 0.1).map(dr => dr.z).sort((a, b) => a - b)
    let start = cz - d / 2
    for (const dz of cuts) { if (dz - GAP > start) segs.push({ x, z: (start + dz - GAP) / 2, len: dz - GAP - start, axis: 'z' }); start = Math.max(start, dz + GAP) }
    if (cz + d / 2 > start) segs.push({ x, z: (start + cz + d / 2) / 2, len: cz + d / 2 - start, axis: 'z' })
  }
  return (
    <group>
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[cx, 0.01, cz]}>
        <planeGeometry args={[w, d]} />
        <meshStandardMaterial color={floorColor} transparent opacity={0.7} />
      </mesh>
      {segs.map((s, i) => (
        <mesh key={i} position={[s.x, h / 2, s.z]}>
          <boxGeometry args={s.axis === 'x' ? [Math.max(s.len, 0.01), h, T] : [T, h, Math.max(s.len, 0.01)]} />
          <meshStandardMaterial color="#39506e" transparent opacity={0.45} />
        </mesh>
      ))}
      <Html position={[cx, 0.25, cz]} center distanceFactor={22} style={{ pointerEvents: 'none' }}>
        <div style={{ color: '#c9d1d9', background: 'rgba(13,17,23,0.72)', border: '1px solid #30363d', borderRadius: 5, padding: '2px 8px', fontSize: 12, fontWeight: 700, whiteSpace: 'nowrap' }}>
          {prettyType(room.type)}
        </div>
      </Html>
    </group>
  )
}

function FollowController({ follow, focus }) {
  const controls = useThree(s => s.controls)
  useFrame(() => {
    if (!follow || !focus || !controls) return
    controls.target.lerp(new THREE.Vector3(focus.x, 1, focus.z), 0.12)
    controls.update()
  })
  return null
}

export default function Replay3D({ frames = [], time = 0, layout = null }) {
  const [follow, setFollow] = useState(false)

  const tracks = useMemo(() => {
    const byId = new Map()
    for (const f of frames) {
      if (!byId.has(f.actorId)) byId.set(f.actorId, [])
      byId.get(f.actorId).push({ t: f.timestamp, x: f.position?.x ?? 0, y: f.position?.y ?? 0, z: f.position?.z ?? 0, ry: f.rotation?.y ?? 0, state: f.currentState || 'Unknown' })
    }
    for (const arr of byId.values()) arr.sort((a, b) => a.t - b.t)
    return byId
  }, [frames])

  const center = useMemo(() => {
    if (layout?.rooms?.length) {
      let sx = 0, sz = 0, n = 0
      for (const r of layout.rooms) if (sane(r.centerX) && sane(r.centerZ)) { sx += r.centerX; sz += r.centerZ; n++ }
      if (n) return [sx / n, sz / n]
    }
    return [0, 0]
  }, [layout])

  const actors = [...tracks.entries()].map(([id, track]) => {
    const pose = poseAt(track, time)
    const prev = poseAt(track, Math.max(0, time - 0.4))
    let moving = false
    if (pose && prev) moving = Math.hypot(pose.x - prev.x, pose.z - prev.z) / 0.4 > 0.25
    return { id, pose, clip: clipFor(kindOf(id), pose?.state, moving) }
  })
  const trainee = actors.find(a => kindOf(a.id) === 'trainee')?.pose || null

  return (
    <div style={{ position: 'relative', width: '100%', height: '100%' }}>
      <button onClick={() => setFollow(f => !f)} style={{
        position: 'absolute', zIndex: 2, top: 10, right: 10, padding: '6px 12px', fontSize: 12, fontWeight: 600,
        borderRadius: 6, cursor: 'pointer', border: `1px solid ${follow ? '#1f6feb' : '#30363d'}`,
        background: follow ? '#1f6feb' : '#161b22', color: follow ? '#fff' : '#8b949e',
      }}>
        {follow ? '● Following trainee' : 'Follow trainee'}
      </button>

      <Canvas camera={{ position: [center[0] + 6, 14, center[1] + 16], fov: 50 }} style={{ width: '100%', height: '100%' }}>
        <color attach="background" args={['#0d1117']} />
        <ambientLight intensity={0.8} />
        <directionalLight position={[12, 22, 8]} intensity={1.1} />
        <mesh rotation={[-Math.PI / 2, 0, 0]} position={[center[0], -0.02, center[1]]}>
          <planeGeometry args={[300, 300]} />
          <meshStandardMaterial color="#0f141b" />
        </mesh>
        <Grid args={[120, 120]} position={[center[0], 0, center[1]]} cellColor="#22272e" sectionColor="#3a4149" fadeDistance={70} infiniteGrid />

        {layout?.rooms?.map((r, i) => <RoomWalls key={'r' + i} room={r} doors={layout.doors} />)}
        {layout?.doors?.map((dr, i) => <Door key={'d' + i} door={dr} />)}
        {actors.map(a => <Actor key={a.id} actorId={a.id} pose={a.pose} clip={a.clip} />)}

        <OrbitControls target={[center[0], 1, center[1]]} makeDefault />
        <FollowController follow={follow} focus={trainee} />
      </Canvas>
    </div>
  )
}
