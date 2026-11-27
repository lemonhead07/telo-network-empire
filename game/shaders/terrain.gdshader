// The planet's ground (LOOK1). Built by PlanetBuilder: smooth normals on hills (crisp at cliffs and cuttings); each
// triangle carries its three corners' biomes (CUSTOM0) and each corner its barycentric place (COLOR), so every spot
// takes the nearest corner's ground with a noisy edge (crisp, natural borders instead of a 16 m smear).
// Detail comes from one tiling 3D noise texture made in code, sampled at several sizes in world space,
// so there are no image files and no seams round the sphere. `detail` follows Settings: 0 Low, 1 Medium, 2 High.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler3D noise3d : filter_linear, repeat_enable;
uniform int detail = 1;
uniform vec3 biome_col[9];    // linear colour of each Biome (PlanetGen's enum order)
uniform vec2 biome_kind[9];   // x: how grassy, y: rocky where steep
uniform float planet_r = 2500.0;
uniform vec3 rock_color : source_color = vec3(0.50, 0.47, 0.45);
uniform vec3 dry_tint : source_color = vec3(0.80, 0.78, 0.42);   // sun-bleached patches of grass
uniform vec3 lush_tint : source_color = vec3(0.30, 0.52, 0.30);  // damp hollows

varying vec3 wpos;
varying vec3 bary;
varying vec3 corner_biome;

void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    bary = COLOR.rgb;
    corner_biome = CUSTOM0.rgb * 255.0;
}

void fragment() {
    float dist = length(VERTEX);
    vec3 up = normalize(wpos);
    vec3 n_world = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
    float flat_ = dot(n_world, up);   // 1 on level ground, 0 on a wall

    // big patches (~100 m): the same everywhere, cheap, and what you see from the air
    float far_n = texture(noise3d, wpos / 900.0).r;
    float mid_n = 0.5, fine_n = 0.5, blade = 0.5;
    if (detail > 0) {
        float fade_mid = 1.0 - smoothstep(350.0, 700.0, dist);
        mid_n = mix(0.5, texture(noise3d, wpos / 70.0 + vec3(0.37)).r, fade_mid);
        float fade_fine = 1.0 - smoothstep(18.0, float(detail > 1 ? 70 : 45), dist);
        if (fade_fine > 0.0) fine_n = mix(0.5, texture(noise3d, wpos / 6.5 + vec3(0.71)).r, fade_fine);
        // up close, a speckle the size of a tuft of grass
        float fade_blade = 1.0 - smoothstep(6.0, 16.0, dist);
        if (fade_blade > 0.0) blade = mix(0.5, smoothstep(0.25, 0.75, texture(noise3d, wpos / 0.7 + vec3(0.13)).r), fade_blade);
    }

    // whose ground is this: the nearest corner of the triangle, its edge pushed about by the noise
    ivec3 id = clamp(ivec3(round(corner_biome)), ivec3(0), ivec3(8));   // never read outside the table
    vec3 b = bary;
    if (detail > 0) {   // Low keeps the borders crisp but straight: two samples fewer
        float w1 = texture(noise3d, wpos / 38.0 + vec3(0.21)).r, w2 = texture(noise3d, wpos / 29.0 + vec3(0.63)).r;
        b += vec3(w1 - 0.5, w2 - 0.5, 1.0 - w1 - w2) * 0.6;
    }
    float m = max(b.x, max(b.y, b.z));
    vec3 w = smoothstep(vec3(m - 0.05), vec3(m), b);
    w /= (w.x + w.y + w.z);
    vec3 base = biome_col[id.x] * w.x + biome_col[id.y] * w.y + biome_col[id.z] * w.z;
    vec2 kind = biome_kind[id.x] * w.x + biome_kind[id.y] * w.y + biome_kind[id.z] * w.z;
    float rocky = dot(bary, vec3(biome_kind[id.x].y, biome_kind[id.y].y, biome_kind[id.z].y));   // smooth: steepness decides
    base *= 1.0 + clamp(length(wpos) - planet_r, 0.0, 150.0) / 600.0;   // a touch lighter up high

    vec3 col = base;
    // grass: dry and lush patches, then a mottle that reads as tufts up close
    float grass = kind.x;
    float dry = smoothstep(0.52, 0.78, far_n + (mid_n - 0.5) * 0.35);
    float lush = 1.0 - smoothstep(0.22, 0.48, far_n + (mid_n - 0.5) * 0.3);
    vec3 g = col;
    g = mix(g, g * dry_tint * 1.75, dry * 0.45);
    g = mix(g, g * lush_tint * 2.2, lush * 0.40);
    g *= 0.90 + 0.20 * mid_n;
    g *= 0.86 + 0.28 * fine_n;
    g *= 0.82 + 0.36 * blade;
    // other ground (sand, snow, seabed): a gentler variation
    vec3 o = col * (0.94 + 0.10 * far_n + 0.08 * (mid_n - 0.5) + 0.10 * (fine_n - 0.5));
    col = mix(o, g, grass);

    // rock where the ground is steep, its edge broken up by the noise
    float edge = flat_ + (mid_n - 0.5) * 0.12 + (far_n - 0.5) * 0.06 + (fine_n - 0.5) * 0.06;
    float rock = (1.0 - smoothstep(0.75, 0.80, edge)) * rocky;
    vec3 stone = rock_color * (0.80 + 0.35 * mid_n) * (0.88 + 0.24 * fine_n);
    col = mix(col, stone, rock);

    ALBEDO = col;
    ROUGHNESS = mix(0.94, 0.80, rock);
    SPECULAR = 0.3;
}
