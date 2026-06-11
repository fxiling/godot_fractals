shader_type canvas_item;

uniform float zoom = 1.5;
uniform int max_iterations = 700;
uniform float aspect_ratio = 1.0;
uniform sampler2D reference_orbit;
uniform vec2 pan_float; 

void fragment() {
    vec2 st = (UV - vec2(0.5)) * 2.0;
    st.x *= aspect_ratio;
    
    vec2 delta_c = st * zoom;
    vec2 c_float = pan_float + delta_c;
    
    vec2 z = vec2(0.0);
    vec2 delta_z = vec2(0.0);
    int iterations = 0;
    bool use_standard_math = false;
    
    for (int i = 0; i < 2000; i++) {
        if (i >= max_iterations) break;
        
        if (!use_standard_math) {
            // Read exact reference state for this step
            vec4 tex_data = texelFetch(reference_orbit, ivec2(i, 0), 0);
            vec2 z_ref = tex_data.xy;
            bool ref_dead = tex_data.b > 0.5; // Blue channel flag
            
            // Perfect mathematical Z for the CURRENT step
            z = z_ref + delta_z;
            
            if (ref_dead) {
                // The reference orbit died. Hand off the perfect Z to standard math.
                use_standard_math = true;
            } else {
                // Check escape for the current step
                if (dot(z, z) > 256.0) break;
                
                // Calculate the delta for the NEXT step
                vec2 z_ref_delta_z = vec2(2.0 * z_ref.x + delta_z.x, 2.0 * z_ref.y + delta_z.y);
                float next_delta_z_x = (z_ref_delta_z.x * delta_z.x - z_ref_delta_z.y * delta_z.y) + delta_c.x;
                float next_delta_z_y = (z_ref_delta_z.x * delta_z.y + z_ref_delta_z.y * delta_z.x) + delta_c.y;
                delta_z = vec2(next_delta_z_x, next_delta_z_y);
            }
        }
        
        if (use_standard_math) {
            // Standard math takes over exactly where perturbation left off
            if (dot(z, z) > 256.0) break;
            
            float next_x = (z.x * z.x - z.y * z.y) + c_float.x;
            float next_y = (2.0 * z.x * z.y) + c_float.y;
            z = vec2(next_x, next_y);
        }
        
        iterations++;
    }
    
    if (iterations == max_iterations) {
        COLOR = vec4(0.0, 0.0, 0.0, 1.0); 
    } else {
        // Because Z is now flawlessly synced, the log math will output perfectly smooth gradients
        float log_zn = log(dot(z, z)) / 2.0;
        float nu = log(log_zn / log(2.0)) / log(2.0);
        float smooth_i = float(iterations) + 1.0 - nu;
        
        float t = smooth_i * 0.03;
        vec3 color = 0.5 + 0.5 * cos(2.0 * 3.14159 * (vec3(1.0) * t + vec3(0.0, 0.3, 0.6)));
        
        COLOR = vec4(color, 1.0);
    }
}
