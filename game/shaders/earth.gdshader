// Bare, trodden earth laid over the ground (LOOK1): along road verges and under hedgerows. Built by GroundDecal:
// vertex alpha is how much earth shows (1 at the road's edge or the hedge's foot, 0 where it fades into the grass);
// the same 3D noise as the ground breaks the edge up so it never reads as a stripe.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, diffuse_burley;

uniform sampler3D noise3d : filter_linear, repeat_enable;
uniform vec3 earth : source_color = vec3(0.34, 0.29, 0.20);
uniform vec3 worn : source_color = vec3(0.42, 0.45, 0.25);   // flattened, yellowed grass between earth and lawn

varying vec3 wpos;
varying float amount;

void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    amount = COLOR.a;
}

void fragment() {
    float n = texture(noise3d, wpos / 11.0).r;
    float f = texture(noise3d, wpos / 1.4 + vec3(0.5)).r;
    float a = clamp(amount * (0.55 + 0.9 * n) + (f - 0.5) * 0.25, 0.0, 1.0);
    ALBEDO = mix(worn, earth, smoothstep(0.45, 0.85, a)) * (0.85 + 0.3 * f);
    ALPHA = smoothstep(0.05, 0.6, a) * 0.92;
    ROUGHNESS = 0.96;
}
