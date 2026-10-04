// The 3D scene behind the page's heading: Kamarajar Street at night, built
// from the game's own pixel art standing up like a paper diorama. The
// streetlights come on one by one, minminis drift about, and Minnal hovers
// over the road. It is decoration only: if WebGL is missing the page shows a
// screenshot instead and everything else still works.
import * as THREE from "./vendor/three.module.min.js";

const canvas = document.getElementById("scene");
const calm = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

let renderer;
try {
  renderer = new THREE.WebGLRenderer({ canvas, antialias: false, alpha: false });
} catch (error) {
  canvas.remove(); // the CSS background screenshot shows through
}

if (renderer) {
  const PIXELS_PER_UNIT = 16; // the game's art is 16 pixels to a metre
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x070a18);
  scene.fog = new THREE.Fog(0x070a18, 26, 60);

  const camera = new THREE.PerspectiveCamera(42, 1, 0.1, 100);
  const loader = new THREE.TextureLoader();

  // A sprite from the game, standing upright on the ground and lit by the lamps.
  function standee(name, x, z, { lit = true, scale = 1 } = {}) {
    const mesh = new THREE.Mesh(
      new THREE.PlaneGeometry(1, 1),
      lit ? new THREE.MeshLambertMaterial({ transparent: true, alphaTest: 0.5 })
          : new THREE.MeshBasicMaterial({ transparent: true, alphaTest: 0.05 }));
    loader.load(`assets/sprites/${name}.png`, (texture) => {
      texture.magFilter = THREE.NearestFilter; // keep the pixels crisp
      texture.minFilter = THREE.NearestFilter;
      texture.colorSpace = THREE.SRGBColorSpace;
      mesh.material.map = texture;
      mesh.material.needsUpdate = true;
      const width = texture.image.width / PIXELS_PER_UNIT * scale;
      const height = texture.image.height / PIXELS_PER_UNIT * scale;
      mesh.scale.set(width, height, 1);
      mesh.position.set(x, height / 2, z);
    });
    scene.add(mesh);
    return mesh;
  }

  // ---- the ground: grass, a strip of pavement, and the road
  function slab(colour, width, depth, z, y = 0) {
    const mesh = new THREE.Mesh(new THREE.PlaneGeometry(width, depth), new THREE.MeshLambertMaterial({ color: colour }));
    mesh.rotation.x = -Math.PI / 2;
    mesh.position.set(0, y, z);
    scene.add(mesh);
  }
  slab(0x2f5a2e, 120, 80, -10);        // grass everywhere
  slab(0x8f8a80, 120, 2.2, 1.6, 0.01); // doorsteps
  slab(0x3d3f4c, 120, 5, 5.2, 0.01);   // Kamarajar Street
  slab(0x7c6044, 120, 1.2, 8.3, 0.01); // dusty shoulder

  // ---- houses along the far side, trees behind and in front
  const houses = ["house_0", "house_tall_0", "house_2", "tea_stall", "house_1", "house_tall_1", "house_3", "house_0", "house_2"];
  houses.forEach((name, i) => standee(name, (i - 4) * 5.2, 0));
  standee("shrine", 13.4, -3.2);
  standee("banyan", -15.5, -4.5);
  [-23, -11.5, -2.4, 6.2, 17, 25].forEach((x, i) => standee(`coconut_${i % 3}`, x, -2.6 - (i % 2) * 1.5));
  [-19, -7, 4.5, 15.5].forEach((x, i) => standee(`coconut_${(i + 1) % 3}`, x, 9.4 + (i % 2)));

  // ---- the kids, Paati, the lineman and Battery
  standee("paati", -3.4, 1.5);
  standee("lineman", -6.3, 1.8);
  standee("kavin_idle", -0.8, 4.6);
  standee("yazhini_idle", 0.6, 5.1);
  standee("nila_idle", 2.1, 4.4);
  standee("dog_idle", 3.3, 5.3);

  // ---- light: dim blue moonlight, then whatever the lamps add
  scene.add(new THREE.AmbientLight(0x5a6ec8, 0.55));
  const moon = new THREE.DirectionalLight(0x7f95e6, 0.35);
  moon.position.set(-6, 10, 8);
  scene.add(moon);

  const torch = new THREE.SpotLight(0xffe9b8, 26, 16, 0.5, 0.6, 1.4);
  torch.position.set(0.6, 1, 5.2);
  torch.target.position.set(0.6, 1.2, -2);
  scene.add(torch, torch.target);

  // Streetlights: dead at first, then they wake up one after another, over and over.
  const lamps = [-13, -4.4, 4.2, 12.8].map((x, i) => {
    const dead = standee("streetlight_off", x, 2.6);
    const live = standee("streetlight_on", x, 2.6, { lit: false });
    live.visible = false;
    const glow = new THREE.PointLight(0xffc878, 0, 13, 1.6);
    glow.position.set(x + 0.4, 3.1, 3.4);
    scene.add(glow);
    return { dead, live, glow, wakesAt: 2.5 + i * 2.2 };
  });
  const CYCLE = 15; // seconds from all dark, through all lit, and back

  // ---- Minnal, the little lightning bolt, hovering over the road
  const minnal = standee("minnal", 7.5, 4.2, { lit: false, scale: 1.2 });
  const spark = new THREE.PointLight(0xfff0a0, 14, 9, 1.8);
  scene.add(spark);

  // ---- minminis and stars: clouds of soft dots
  function dots(count, colour, size, place) {
    const positions = new Float32Array(count * 3);
    for (let i = 0; i < count; i++) positions.set(place(), i * 3);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));
    const material = new THREE.PointsMaterial({
      color: colour, size, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
      map: loader.load("assets/sprites/glow.png"),
    });
    const points = new THREE.Points(geometry, material);
    scene.add(points);
    return points;
  }
  const spread = (range) => (Math.random() - 0.5) * range;
  const minminis = dots(170, 0xc8ff66, 0.42, () => [spread(52), 0.4 + Math.random() * 3.4, spread(16) + 4]);
  const homes = minminis.geometry.attributes.position.array.slice();
  dots(260, 0xbfd0ff, 0.5, () => [spread(160), 14 + Math.random() * 30, -36 - Math.random() * 10]);

  // ---- camera: drifts along the street, leans with the mouse, lifts as you scroll
  const pointer = { x: 0, y: 0 };
  window.addEventListener("pointermove", (event) => {
    pointer.x = event.clientX / window.innerWidth - 0.5;
    pointer.y = event.clientY / window.innerHeight - 0.5;
  }, { passive: true });

  function resize() {
    const width = canvas.clientWidth, height = canvas.clientHeight;
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.fov = width < height ? 62 : 38; // show more of the street on a tall phone screen
    camera.updateProjectionMatrix();
  }
  window.addEventListener("resize", resize);
  resize();

  const clock = new THREE.Clock();
  function frame() {
    const t = calm ? 9 : clock.getElapsedTime();
    const scrolled = Math.min(1, window.scrollY / window.innerHeight);

    camera.position.set(Math.sin(t * 0.12) * 6 + pointer.x * 2.5, 5.2 + scrolled * 6 - pointer.y * 0.8, 23 + scrolled * 5);
    camera.lookAt(Math.sin(t * 0.12) * 3, 1.2, 0);

    const phase = t % CYCLE;
    for (const lamp of lamps) {
      const on = phase > lamp.wakesAt && phase < CYCLE - 1.2;
      lamp.dead.visible = !on;
      lamp.live.visible = on;
      const sinceWake = phase - lamp.wakesAt;
      const flash = on ? 1 + Math.max(0, 1 - sinceWake * 2) * 2.5 : 0; // a bright pop as it comes on
      lamp.glow.intensity = 30 * flash;
    }

    minnal.position.x = 7.5 + Math.sin(t * 0.7) * 1.4;
    minnal.position.y = 2.9 + Math.sin(t * 2.3) * 0.25;
    spark.position.copy(minnal.position);
    spark.intensity = 12 + Math.sin(t * 17) * 3 + Math.sin(t * 7.3) * 2;

    const spots = minminis.geometry.attributes.position.array;
    for (let i = 0; i < spots.length; i += 3) {
      spots[i] = homes[i] + Math.sin(t * 0.5 + i) * 0.7;
      spots[i + 1] = homes[i + 1] + Math.sin(t * 0.9 + i * 1.7) * 0.35;
      spots[i + 2] = homes[i + 2] + Math.cos(t * 0.4 + i * 0.6) * 0.7;
    }
    minminis.geometry.attributes.position.needsUpdate = true;

    renderer.render(scene, camera);
  }

  // Draw only while the heading is on screen and the tab is visible.
  let visible = true;
  new IntersectionObserver(([entry]) => { visible = entry.isIntersecting; }).observe(canvas);
  renderer.setAnimationLoop(() => {
    if (visible && !document.hidden) frame();
  });
  if (calm) {
    renderer.setAnimationLoop(null);
    setTimeout(frame, 600); // one still picture, once the art has loaded
    setTimeout(frame, 2000);
  }
}
