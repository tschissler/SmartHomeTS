class MqttTopics {
  // LED / Illumination
  static const String ledStripe = 'commands/illumination/LEDStripe/setColor';
  static const String lamp = 'commands/shelly/Lampe';

  // Charging
  static const String chargingWildcard = 'data/charging/#';
  static const String chargingSituation = 'data/charging/situation';
  static const String chargingBmw = 'data/charging/BMW';
  static const String chargingMini = 'data/charging/Mini';
  static const String chargingVw = 'data/charging/VW';
  static const String chargingSettings = 'config/charging/settings';

  // Heating
  static const String heatingWildcard = 'commands/Heating/#';
  static const String heatingKinderzimmer =
      'commands/Heating/Heizkörperlüfter_Kinderzimmer';
  static const String heatingEsszimmer =
      'commands/Heating/Heizkörperlüfter_Esszimmer';

  // Climate – temperature
  static const String tempKeller = 'daten/temperatur/M1/Keller';
  static const String tempAussen = 'daten/temperatur/M1/Aussen';
  static const String tempKinderzimmer = 'daten/temperatur/M1/Kinderzimmer';
  static const String tempBad = 'daten/temperatur/M1/Bad';
  static const String tempWohnzimmer = 'daten/temperatur/M1/Wohnzimmer';
  static const String tempSchlafzimmer = 'daten/temperatur/M1/Schlafzimmer';

  // Climate – humidity
  static const String humKeller = 'daten/luftfeuchtigkeit/M1/Keller';
  static const String humAussen = 'daten/luftfeuchtigkeit/M1/Aussen';
  static const String humKinderzimmer =
      'daten/luftfeuchtigkeit/M1/Kinderzimmer';
  static const String humBad = 'daten/luftfeuchtigkeit/M1/Bad';
  static const String humWohnzimmer = 'daten/luftfeuchtigkeit/M1/Wohnzimmer';
  static const String humSchlafzimmer =
      'daten/luftfeuchtigkeit/M1/Schlafzimmer';

  // Climate – cistern
  static const String cistern = 'daten/zisterneFuellstand/M1/Keller';

  // Notifications
  static const String nachrichtenWildcard = 'Nachrichten/#';

  /// All topics to subscribe at connect time.
  static const List<String> subscriptions = [
    ledStripe,
    lamp,
    chargingWildcard,
    chargingSettings,
    'daten/#',
    heatingWildcard,
    nachrichtenWildcard,
  ];
}
