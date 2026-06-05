shader_type canvas_item;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	COLOR = vec4(0.0, 0.0, 0.0, texture(TEXTURE, UV).a);
}