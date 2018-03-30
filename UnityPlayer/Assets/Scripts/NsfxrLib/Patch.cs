/// Library to generate programmatic sounds for games
/// Converted to C# by Polomino Games for use in Puzzlang

using System;
using System.Linq;

namespace NsfxrLib {
  public enum Wave {
    SQUARE,
    SAWTOOTH,
    SINE,
    NOISE,
    TRIANGLE,
    BREAKER,
    SIZE
  }

  public class Patch {
    public Wave wave_type = Wave.SQUARE;
    public double env_attack = 0.0;
    public double env_sustain = 0.3;
    public double env_punch = 0.0;
    public double env_decay = 0.4;
    public double base_freq = 0.3;
    public double freq_limit = 0.0;
    public double freq_ramp = 0.0;
    public double freq_dramp = 0.0;
    public double vib_strength = 0.0;
    public double vib_speed = 0.0;
    public double arp_mod = 0.0;
    public double arp_speed = 0.0;
    public double duty = 0.0;
    public double duty_ramp = 0.0;
    public double repeat_speed = 0.0;
    public double pha_offset = 0.0;
    public double pha_ramp = 0.0;
    public double lpf_freq = 1.0;
    public double lpf_ramp = 0.0;
    public double lpf_resonance = 0.0;
    public double hpf_freq = 0.0;
    public double hpf_ramp = 0.0;

    public override string ToString() {
      return ConcatParams(
        "\n wave_type     = ", (int)wave_type,
        "\n env_attack    = ", env_attack,
        "\n env_sustain   = ", env_sustain,
        "\n env_punch     = ", env_punch,
        "\n env_decay     = ", env_decay,
        "\n base_freq     = ", base_freq,
        "\n freq_limit    = ", freq_limit,
        "\n freq_ramp     = ", freq_ramp,
        "\n freq_dramp    = ", freq_dramp,
        "\n vib_strength  = ", vib_strength,
        "\n vib_speed     = ", vib_speed,
        "\n arp_mod       = ", arp_mod,
        "\n arp_speed     = ", arp_speed,
        "\n duty          = ", duty,
        "\n duty_ramp     = ", duty_ramp,
        "\n repeat_speed  = ", repeat_speed,
        "\n pha_offset    = ", pha_offset,
        "\n pha_ramp      = ", pha_ramp,
        "\n lpf_freq      = ", lpf_freq,
        "\n lpf_ramp      = ", lpf_ramp,
        "\n lpf_resonance = ", lpf_resonance,
        "\n hpf_freq      = ", hpf_freq,
        "\n hpf_ramp      = ", hpf_ramp);
    }

    string ConcatParams(params object[] args) {
      return String.Concat(args.Select(o => o.ToString()).ToArray());
    }
  }
}