shader_type canvas_item;

// Controla la apertura del círculo (0.0 = completamente cerrado/negro, 1.0 = completamente abierto)
uniform float radio : hint_range(0.0, 1.0) = 1.0;

void fragment() {
    // Calcular el centro de la pantalla
    vec2 centro = vec2(0.5, 0.5);
    
    // Ajustar la distancia para que el círculo no se estire si la pantalla es panorámica
    vec2 uv = UV;
    uv.x = (uv.x - 0.5) * 1.777 + 0.5; // Supone una relación de aspecto 16:9
    
    float distancia = distance(uv, centro);
    
    // Si la distancia al centro es mayor que el radio, pintamos de negro (alfa 1.0)
    // Si es menor, lo hacemos completamente transparente (alfa 0.0)
    if (distancia > radio) {
        COLOR = vec4(0.0, 0.0, 0.0, 1.0);
    } else {
        COLOR = vec4(0.0, 0.0, 0.0, 0.0);
    }
}