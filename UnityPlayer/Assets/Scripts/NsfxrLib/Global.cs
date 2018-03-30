/// Library to generate programmatic sounds for games
/// Converted to C# by Polomino Games for use in Puzzlang

/// Aims to be compatible with PuzzleScript sounds
///     https://www.puzzlescript.net/
/// 
/// Based on BFXR by increpare
///     https://www.bfxr.net/
/// 
/// Based on the origina SFXR by Thomas Petersson
///     http://www.drpetter.se/project_sfxr.html

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace NsfxrLib {
  public static class Global {
    public const double PI = 3.141592653589793238462643383279502884;
    public const double SampleRate = 44100.0;

    public static Patch PickupCoin(RNG rng) {
      Patch result = new Patch();
      result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
      if (result.wave_type == Wave.NOISE) {
        result.wave_type = Wave.SQUARE;
      }
      result.base_freq = 0.4 + rng.Frnd(0.5);
      result.env_attack = 0.0;
      result.env_sustain = rng.Frnd(0.1);
      result.env_decay = 0.1 + rng.Frnd(0.4);
      result.env_punch = 0.3 + rng.Frnd(0.3);
      if (rng.Rnd(1) != 0) {
        result.arp_speed = 0.5 + rng.Frnd(0.2);
        float num = (rng.Irnd(7) | 1) + 1;
        float den = num + (rng.Irnd(7) | 1) + 2;
        result.arp_mod = num / den;
      }
      return result;
    }

    public static Patch LaserShoot(RNG rng) {
      Patch result = new Patch();
      result.wave_type = (Wave)rng.Rnd(2);
      if (result.wave_type == Wave.SINE && rng.Rnd(1) != 0) {
        result.wave_type = (Wave)rng.Rnd(1);
      }
      result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
      if (result.wave_type == Wave.NOISE) {
        result.wave_type = Wave.SQUARE;
      }
      result.base_freq = 0.5 + rng.Frnd(0.5);
      result.freq_limit = result.base_freq - 0.2 - rng.Frnd(0.6);
      if (result.freq_limit < 0.2) {
        result.freq_limit = 0.2;
      }
      result.freq_ramp = -0.15 - rng.Frnd(0.2);
      if (rng.Rnd(2) == 0) {
        result.base_freq = 0.3 + rng.Frnd(0.6);
        result.freq_limit = rng.Frnd(0.1);
        result.freq_ramp = -0.35 - rng.Frnd(0.3);
      }
      if (rng.Rnd(1) != 0) {
        result.duty = rng.Frnd(0.5);
        result.duty_ramp = rng.Frnd(0.2);
      } else {
        result.duty = 0.4 + rng.Frnd(0.5);
        result.duty_ramp = -rng.Frnd(0.7);
      }
      result.env_attack = 0.0;
      result.env_sustain = 0.1 + rng.Frnd(0.2);
      result.env_decay = rng.Frnd(0.4);
      if (rng.Rnd(1) != 0) {
        result.env_punch = rng.Frnd(0.3);
      }
      if (rng.Rnd(2) == 0) {
        result.pha_offset = rng.Frnd(0.2);
        result.pha_ramp = -rng.Frnd(0.2);
      }
      if (rng.Rnd(1) != 0) {
        result.hpf_freq = rng.Frnd(0.3);
      }
      return result;
    }

    public static Patch Explosion(RNG rng) {
      Patch result = new Patch();
      if (rng.Rnd(1) != 0) {
        result.base_freq = 0.1 + rng.Frnd(0.4);
        result.freq_ramp = -0.1 + rng.Frnd(0.4);
      } else {
        result.base_freq = 0.2 + rng.Frnd(0.7);
        result.freq_ramp = -0.2 - rng.Frnd(0.2);
      }
      result.base_freq *= result.base_freq;
      if (rng.Rnd(4) == 0) {
        result.freq_ramp = 0.0;
      }
      if (rng.Rnd(2) == 0) {
        result.repeat_speed = 0.3 + rng.Frnd(0.5);
      }
      result.env_attack = 0.0;
      result.env_sustain = 0.1 + rng.Frnd(0.3);
      result.env_decay = rng.Frnd(0.5);
      if (rng.Rnd(1) == 0) {
        result.pha_offset = -0.3 + rng.Frnd(0.9);
        result.pha_ramp = -rng.Frnd(0.3);
      }
      result.env_punch = 0.2 + rng.Frnd(0.6);
      if (rng.Rnd(1) != 0) {
        result.vib_strength = rng.Frnd(0.7);
        result.vib_speed = rng.Frnd(0.6);
      }
      if (rng.Rnd(2) == 0) {
        result.arp_speed = 0.6 + rng.Frnd(0.3);
        result.arp_mod = 0.8 - rng.Frnd(1.6);
      }
      return result;
    }

    public static Patch PowerUp(RNG rng) {
      Patch result = new Patch();
      if (rng.Rnd(1) != 0) {
        result.wave_type = Wave.SAWTOOTH;
      } else {
        result.duty = rng.Frnd(0.6);
      }
      result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
      if (result.wave_type == Wave.NOISE) {
        result.wave_type = Wave.SQUARE;
      }
      if (rng.Rnd(1) != 0) {
        result.base_freq = 0.2 + rng.Frnd(0.3);
        result.freq_ramp = 0.1 + rng.Frnd(0.4);
        result.repeat_speed = 0.4 + rng.Frnd(0.4);
      } else {
        result.base_freq = 0.2 + rng.Frnd(0.3);
        result.freq_ramp = 0.05 + rng.Frnd(0.2);
        if (rng.Rnd(1) != 0) {
          result.vib_strength = rng.Frnd(0.7);
          result.vib_speed = rng.Frnd(0.6);
        }
      }
      result.env_attack = 0.0;
      result.env_sustain = rng.Frnd(0.4);
      result.env_decay = 0.1 + rng.Frnd(0.4);
      return result;
    }

    public static Patch HitHurt(RNG rng) {
      Patch result = new Patch();
      result.wave_type = (Wave)rng.Rnd(2);
      if (result.wave_type == Wave.SINE) {
        result.wave_type = Wave.NOISE;
      }
      if (result.wave_type == Wave.SQUARE) {
        result.duty = rng.Frnd(0.6);
      }
      result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
      result.base_freq = 0.2 + rng.Frnd(0.6);
      result.freq_ramp = -0.3 - rng.Frnd(0.4);
      result.env_attack = 0.0;
      result.env_sustain = rng.Frnd(0.1);
      result.env_decay = 0.1 + rng.Frnd(0.2);
      if (rng.Rnd(1) != 0) {
        result.hpf_freq = rng.Frnd(0.3);
      }
      return result;
    }

    public static Patch Jump(RNG rng) {
      Patch result = new Patch();
      result.wave_type = Wave.SQUARE;
      result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
      if (result.wave_type == Wave.NOISE) {
        result.wave_type = Wave.SQUARE;
      }
      result.duty = rng.Frnd(0.6);
      result.base_freq = 0.3 + rng.Frnd(0.3);
      result.freq_ramp = 0.1 + rng.Frnd(0.2);
      result.env_attack = 0.0;
      result.env_sustain = 0.1 + rng.Frnd(0.3);
      result.env_decay = 0.1 + rng.Frnd(0.2);
      if (rng.Rnd(1) != 0) {
        result.hpf_freq = rng.Frnd(0.3);
      }
      if (rng.Rnd(1) != 0) {
        result.lpf_freq = 1.0 - rng.Frnd(0.6);
      }
      return result;
    }

    public static Patch BlipSelect(RNG rng) {
      Patch result = new Patch();
      result.wave_type = (Wave)rng.Rnd(1);
      result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
      if (result.wave_type == Wave.NOISE) {
        result.wave_type = (Wave)rng.Rnd(1);
      }
      if (result.wave_type == Wave.SQUARE) {
        result.duty = rng.Frnd(0.6);
      }
      result.base_freq = 0.2 + rng.Frnd(0.4);
      result.env_attack = 0.0;
      result.env_sustain = 0.1 + rng.Frnd(0.1);
      result.env_decay = rng.Frnd(0.2);
      result.hpf_freq = 0.1;
      return result;
    }

    public static Patch PushSound(RNG rng) {
      Patch result = new Patch();
      result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
      if (result.wave_type == Wave.SINE) {
        result.wave_type = Wave.NOISE;
      }
      if (result.wave_type == Wave.SQUARE) {
        result.wave_type = Wave.NOISE;
      }
      result.base_freq = 0.1 + rng.Frnd(0.4);
      result.freq_ramp = 0.05 + rng.Frnd(0.2);

      result.env_attack = 0.01 + rng.Frnd(0.09);
      result.env_sustain = 0.01 + rng.Frnd(0.09);
      result.env_decay = 0.01 + rng.Frnd(0.09);

      result.repeat_speed = 0.3 + rng.Frnd(0.5);
      result.pha_offset = -0.3 + rng.Frnd(0.9);
      result.pha_ramp = -rng.Frnd(0.3);
      result.arp_speed = 0.6 + rng.Frnd(0.3);
      result.arp_mod = 0.8 - rng.Frnd(1.6);
      return result;
    }

    public static Patch Random(RNG rng) {
      Patch result = new Patch();
      result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
      result.base_freq = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 2.0);
      if (rng.Rnd(1) != 0) {
        result.base_freq = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 3.0) + 0.5;
      }
      result.freq_limit = 0.0;
      result.freq_ramp = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 5.0);
      if (result.base_freq > 0.7 && result.freq_ramp > 0.2) {
        result.freq_ramp = -result.freq_ramp;
      }
      if (result.base_freq < 0.2 && result.freq_ramp < -0.05) {
        result.freq_ramp = -result.freq_ramp;
      }
      result.freq_dramp = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 3.0);
      result.duty = rng.Frnd(2.0) - 1.0;
      result.duty_ramp = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 3.0);
      result.vib_strength = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 3.0);
      result.vib_speed = rng.Frnd(2.0) - 1.0;
      result.env_attack = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 3.0);
      result.env_sustain = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 2.0);
      result.env_decay = rng.Frnd(2.0) - 1.0;
      result.env_punch = (float)Math.Pow(rng.Frnd(0.8), 2.0);
      if (result.env_attack + result.env_sustain + result.env_decay < 0.2) {
        result.env_sustain += 0.2 + rng.Frnd(0.3);
        result.env_decay += 0.2 + rng.Frnd(0.3);
      }
      result.lpf_resonance = rng.Frnd(2.0) - 1.0;
      result.lpf_freq = 1.0 - (float)Math.Pow(rng.Frnd(1.0), 3.0);
      result.lpf_ramp = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 3.0);
      if (result.lpf_freq < 0.1 && result.lpf_ramp < -0.05) {
        result.lpf_ramp = -result.lpf_ramp;
      }
      result.hpf_freq = (float)Math.Pow(rng.Frnd(1.0), 5.0);
      result.hpf_ramp = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 5.0);
      result.pha_offset = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 3.0);
      result.pha_ramp = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 3.0);
      result.repeat_speed = rng.Frnd(2.0) - 1.0;
      result.arp_speed = rng.Frnd(2.0) - 1.0;
      result.arp_mod = rng.Frnd(2.0) - 1.0;
      return result;
    }

    public static Patch Bird(RNG rng) {
      Patch result = new Patch();

      if (rng.Frnd(10) < 1) {
        result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
        if (result.wave_type == Wave.NOISE) {
          result.wave_type = Wave.SQUARE;
        }
        result.env_attack = 0.4304400932967592 + rng.Frnd(0.2) - 0.1;
        result.env_sustain = 0.15739346034252394 + rng.Frnd(0.2) - 0.1;
        result.env_punch = 0.004488201744871758 + rng.Frnd(0.2) - 0.1;
        result.env_decay = 0.07478075528212291 + rng.Frnd(0.2) - 0.1;
        result.base_freq = 0.9865265720147687 + rng.Frnd(0.2) - 0.1;
        result.freq_limit = 0 + rng.Frnd(0.2) - 0.1;
        result.freq_ramp = -0.2995018224359539 + rng.Frnd(0.2) - 0.1;
        if (rng.Frnd(1.0) < 0.5) {
          result.freq_ramp = 0.1 + rng.Frnd(0.15);
        }
        result.freq_dramp = 0.004598608156964473 + rng.Frnd(0.1) - 0.05;
        result.vib_strength = -0.2202799497929496 + rng.Frnd(0.2) - 0.1;
        result.vib_speed = 0.8084998703158364 + rng.Frnd(0.2) - 0.1;
        result.arp_mod = 0;
        result.arp_speed = 0;
        result.duty = -0.9031808754347107 + rng.Frnd(0.2) - 0.1;
        result.duty_ramp = -0.8128699999808343 + rng.Frnd(0.2) - 0.1;
        result.repeat_speed = 0.6014860189319991 + rng.Frnd(0.2) - 0.1;
        result.pha_offset = -0.9424902314367765 + rng.Frnd(0.2) - 0.1;
        result.pha_ramp = -0.1055482222272056 + rng.Frnd(0.2) - 0.1;
        result.lpf_freq = 0.9989765717851521 + rng.Frnd(0.2) - 0.1;
        result.lpf_ramp = -0.25051720626043017 + rng.Frnd(0.2) - 0.1;
        result.lpf_resonance = 0.32777871505494693 + rng.Frnd(0.2) - 0.1;
        result.hpf_freq = 0.0023548750981756753 + rng.Frnd(0.2) - 0.1;
        result.hpf_ramp = -0.002375673204842568 + rng.Frnd(0.2) - 0.1;
        return result;
      }

      if (rng.Frnd(10) < 1) {
        result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
        if (result.wave_type == Wave.NOISE) {
          result.wave_type = Wave.SQUARE;
        }
        result.env_attack = 0.5277795946672003 + rng.Frnd(0.2) - 0.1;
        result.env_sustain = 0.18243733568468432 + rng.Frnd(0.2) - 0.1;
        result.env_punch = -0.020159754546840117 + rng.Frnd(0.2) - 0.1;
        result.env_decay = 0.1561353422051903 + rng.Frnd(0.2) - 0.1;
        result.base_freq = 0.9028855606533718 + rng.Frnd(0.2) - 0.1;
        result.freq_limit = -0.008842787837148716;
        result.freq_ramp = -0.1;
        result.freq_dramp = -0.012891241489551925;
        result.vib_strength = -0.17923136138403065 + rng.Frnd(0.2) - 0.1;
        result.vib_speed = 0.908263385610142 + rng.Frnd(0.2) - 0.1;
        result.arp_mod = 0.41690153355414894 + rng.Frnd(0.2) - 0.1;
        result.arp_speed = 0.0010766233195860703 + rng.Frnd(0.2) - 0.1;
        result.duty = -0.8735363011184684 + rng.Frnd(0.2) - 0.1;
        result.duty_ramp = -0.7397985366747507 + rng.Frnd(0.2) - 0.1;
        result.repeat_speed = 0.0591789344172107 + rng.Frnd(0.2) - 0.1;
        result.pha_offset = -0.9961184222777699 + rng.Frnd(0.2) - 0.1;
        result.pha_ramp = -0.08234769395850523 + rng.Frnd(0.2) - 0.1;
        result.lpf_freq = 0.9412475115697335 + rng.Frnd(0.2) - 0.1;
        result.lpf_ramp = -0.18261358925834958 + rng.Frnd(0.2) - 0.1;
        result.lpf_resonance = 0.24541438107389477 + rng.Frnd(0.2) - 0.1;
        result.hpf_freq = -0.01831940280978611 + rng.Frnd(0.2) - 0.1;
        result.hpf_ramp = -0.03857383633171346 + rng.Frnd(0.2) - 0.1;
        return result;
      }
      if (rng.Frnd(10) < 1) {
        result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
        if (result.wave_type == Wave.NOISE) {
          result.wave_type = Wave.SQUARE;
        }
        result.env_attack = 0.4304400932967592 + rng.Frnd(0.2) - 0.1;
        result.env_sustain = 0.15739346034252394 + rng.Frnd(0.2) - 0.1;
        result.env_punch = 0.004488201744871758 + rng.Frnd(0.2) - 0.1;
        result.env_decay = 0.07478075528212291 + rng.Frnd(0.2) - 0.1;
        result.base_freq = 0.9865265720147687 + rng.Frnd(0.2) - 0.1;
        result.freq_limit = 0 + rng.Frnd(0.2) - 0.1;
        result.freq_ramp = -0.2995018224359539 + rng.Frnd(0.2) - 0.1;
        result.freq_dramp = 0.004598608156964473 + rng.Frnd(0.2) - 0.1;
        result.vib_strength = -0.2202799497929496 + rng.Frnd(0.2) - 0.1;
        result.vib_speed = 0.8084998703158364 + rng.Frnd(0.2) - 0.1;
        result.arp_mod = -0.46410459213693644 + rng.Frnd(0.2) - 0.1;
        result.arp_speed = -0.10955361249587248 + rng.Frnd(0.2) - 0.1;
        result.duty = -0.9031808754347107 + rng.Frnd(0.2) - 0.1;
        result.duty_ramp = -0.8128699999808343 + rng.Frnd(0.2) - 0.1;
        result.repeat_speed = 0.7014860189319991 + rng.Frnd(0.2) - 0.1;
        result.pha_offset = -0.9424902314367765 + rng.Frnd(0.2) - 0.1;
        result.pha_ramp = -0.1055482222272056 + rng.Frnd(0.2) - 0.1;
        result.lpf_freq = 0.9989765717851521 + rng.Frnd(0.2) - 0.1;
        result.lpf_ramp = -0.25051720626043017 + rng.Frnd(0.2) - 0.1;
        result.lpf_resonance = 0.32777871505494693 + rng.Frnd(0.2) - 0.1;
        result.hpf_freq = 0.0023548750981756753 + rng.Frnd(0.2) - 0.1;
        result.hpf_ramp = -0.002375673204842568 + rng.Frnd(0.2) - 0.1;
        return result;
      }
      if (rng.Frnd(5) > 1) {
        result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
        if (result.wave_type == Wave.NOISE) {
          result.wave_type = Wave.SQUARE;
        }
        if (rng.Rnd(1) != 0) {
          result.arp_mod = 0.2697849293151393 + rng.Frnd(0.2) - 0.1;
          result.arp_speed = -0.3131172257760948 + rng.Frnd(0.2) - 0.1;
          result.base_freq = 0.8090588299313949 + rng.Frnd(0.2) - 0.1;
          result.duty = -0.6210022920964955 + rng.Frnd(0.2) - 0.1;
          result.duty_ramp = -0.00043441813553182567 + rng.Frnd(0.2) - 0.1;
          result.env_attack = 0.004321877246874195 + rng.Frnd(0.2) - 0.1;
          result.env_decay = 0.1 + rng.Frnd(0.2) - 0.1;
          result.env_punch = 0.061737781504416146 + rng.Frnd(0.2) - 0.1;
          result.env_sustain = 0.4987252564798832 + rng.Frnd(0.2) - 0.1;
          result.freq_dramp = 0.31700340314222614 + rng.Frnd(0.2) - 0.1;
          result.freq_limit = 0 + rng.Frnd(0.2) - 0.1;
          result.freq_ramp = -0.163380391341416 + rng.Frnd(0.2) - 0.1;
          result.hpf_freq = 0.4709005021145149 + rng.Frnd(0.2) - 0.1;
          result.hpf_ramp = 0.6924667290539194 + rng.Frnd(0.2) - 0.1;
          result.lpf_freq = 0.8351398631384511 + rng.Frnd(0.2) - 0.1;
          result.lpf_ramp = 0.36616557192873134 + rng.Frnd(0.2) - 0.1;
          result.lpf_resonance = -0.08685777111664439 + rng.Frnd(0.2) - 0.1;
          result.pha_offset = -0.036084571580025544 + rng.Frnd(0.2) - 0.1;
          result.pha_ramp = -0.014806445085568108 + rng.Frnd(0.2) - 0.1;
          result.repeat_speed = -0.8094368475518489 + rng.Frnd(0.2) - 0.1;
          result.vib_speed = 0.4496665457171294 + rng.Frnd(0.2) - 0.1;
          result.vib_strength = 0.23413762515532424 + rng.Frnd(0.2) - 0.1;
        } else {
          result.arp_mod = -0.35697118026766184 + rng.Frnd(0.2) - 0.1;
          result.arp_speed = 0.3581140690559588 + rng.Frnd(0.2) - 0.1;
          result.base_freq = 1.3260897696157528 + rng.Frnd(0.2) - 0.1;
          result.duty = -0.30984900436710694 + rng.Frnd(0.2) - 0.1;
          result.duty_ramp = -0.0014374759133411626 + rng.Frnd(0.2) - 0.1;
          result.env_attack = 0.3160357835682254 + rng.Frnd(0.2) - 0.1;
          result.env_decay = 0.1 + rng.Frnd(0.2) - 0.1;
          result.env_punch = 0.24323114016870148 + rng.Frnd(0.2) - 0.1;
          result.env_sustain = 0.4 + rng.Frnd(0.2) - 0.1;
          result.freq_dramp = 0.2866475886237244 + rng.Frnd(0.2) - 0.1;
          result.freq_limit = 0 + rng.Frnd(0.2) - 0.1;
          result.freq_ramp = -0.10956352368742976 + rng.Frnd(0.2) - 0.1;
          result.hpf_freq = 0.20772718017889846 + rng.Frnd(0.2) - 0.1;
          result.hpf_ramp = 0.1564090637378835 + rng.Frnd(0.2) - 0.1;
          result.lpf_freq = 0.6021372770637031 + rng.Frnd(0.2) - 0.1;
          result.lpf_ramp = 0.24016227139979027 + rng.Frnd(0.2) - 0.1;
          result.lpf_resonance = -0.08787383821160144 + rng.Frnd(0.2) - 0.1;
          result.pha_offset = -0.381597686151701 + rng.Frnd(0.2) - 0.1;
          result.pha_ramp = -0.0002481687661373495 + rng.Frnd(0.2) - 0.1;
          result.repeat_speed = 0.07812112809425686 + rng.Frnd(0.2) - 0.1;
          result.vib_speed = -0.13648848579133943 + rng.Frnd(0.2) - 0.1;
          result.vib_strength = 0.0018874158972302657 + rng.Frnd(0.2) - 0.1;
        }
        return result;
      }

      result.wave_type = (Wave)rng.Irnd((int)Wave.SIZE);
      if (result.wave_type == Wave.SAWTOOTH || result.wave_type == Wave.NOISE) {
        result.wave_type = Wave.SINE;
      }
      result.base_freq = 0.85 + rng.Frnd(0.15);
      result.freq_ramp = 0.3 + rng.Frnd(0.15);

      result.env_attack = 0 + rng.Frnd(0.09);
      result.env_sustain = 0.2 + rng.Frnd(0.3);
      result.env_decay = 0 + rng.Frnd(0.1);

      result.duty = rng.Frnd(2.0) - 1.0;
      result.duty_ramp = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 3.0);

      result.repeat_speed = 0.5 + rng.Frnd(0.1);

      result.pha_offset = -0.3 + rng.Frnd(0.9);
      result.pha_ramp = -rng.Frnd(0.3);

      result.arp_speed = 0.4 + rng.Frnd(0.6);
      result.arp_mod = 0.8 + rng.Frnd(0.1);

      result.lpf_resonance = rng.Frnd(2.0) - 1.0;
      result.lpf_freq = 1.0 - (float)Math.Pow(rng.Frnd(1.0), 3.0);
      result.lpf_ramp = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 3.0);
      if (result.lpf_freq < 0.1 && result.lpf_ramp < -0.05) {
        result.lpf_ramp = -result.lpf_ramp;
      }
      result.hpf_freq = (float)Math.Pow(rng.Frnd(1.0), 5.0);
      result.hpf_ramp = (float)Math.Pow(rng.Frnd(2.0) - 1.0, 5.0);
      return result;
    }

    public static void SaveWAV(List<float> samples, string filename) {
      // may raise exception -- trap it if you need to
      var fs = new FileStream(filename, FileMode.Create);

      int num_samples = samples.Count;
      int wav_bits = 16;
      int wav_freq = 44100;

      // write wav header
      Write(fs, "RIFF", 4); // "RIFF"
      Write(fs, 40 - 4 + num_samples * wav_bits / 8, 4); // remaining file size
      Write(fs, "WAVE", 4); // "WAVE"
      Write(fs, "fmt ", 4); // "fmt "
      Write(fs, 16, 4); // chunk size
      Write(fs, 1, 2); // compression code
      Write(fs, 1, 2); // channels
      Write(fs, wav_freq, 4); // sample rate
      Write(fs, wav_freq * wav_bits / 8, 4); // bytes/sec
      Write(fs, wav_bits / 8, 2); // block align
      Write(fs, wav_bits, 2); // bits per sample
      Write(fs, "data", 4); // "data"
      Write(fs, num_samples * wav_bits / 8, 4); // chunk size

      foreach (var sample in samples) {
        short isample = (short)(sample * 32000);
        Write(fs, isample, 2);
      }
      fs.Close();
    }

    static void Write(FileStream fstream, string data, int length) {
      for (int i = 0; i < length; i++)
        fstream.WriteByte(i < data.Length ? (byte)data[i] : (byte)0);
    }

    static void Write(FileStream fstream, int data, int length) {
      for (int i = 0; i < length; i++) {
        fstream.WriteByte((byte)(data & 0xff));
        data >>= 8;
      }
    }

    public static void UnitTests() {
      {
        RNG rng = new RNG(0);
        Debug.Assert(rng.Uniform() == 0.5331977905620839);
        Debug.Assert(rng.Uniform() == 0.1531524589417522);
        for (int i = 0; i < 1024; i++) {
          rng.Uniform();
        }
        Debug.Assert(rng.Uniform() == 0.8312710111387246);
      }
      {
        RNG rng = new RNG(632749);
        Patch p = PushSound(rng);
        Debug.Assert(p.env_decay == 0.08117953194502579);
        Debug.Assert(p.freq_ramp == 0.20779508427941235);
        Debug.Assert(p.wave_type == Wave.SAWTOOTH);
      }
      {
        RNG rng = new RNG(806717);
        Patch p = PickupCoin(rng);
        Debug.Assert(p.env_decay == 0.40795044598372276);
        Debug.Assert(p.base_freq == 0.598671793704112);
        Debug.Assert(p.wave_type == Wave.SQUARE);
      }
    }
  }
}