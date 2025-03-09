import 'package:flutter/material.dart';
import 'package:smarthome_app/models/car_status.dart';
import 'package:smarthome_app/models/car_type.dart';
import 'package:flutter_svg/svg.dart';
import 'package:intl/intl.dart';

class CarStatusWidget extends StatelessWidget {
  final CarType car;
  final CarStatus? carStatus;
  final DateFormat dateTimeFormatter = DateFormat('dd.MM.yy HH:mm:ss');
  final DateFormat timeFormatter = DateFormat('HH:mm');
  final textStyle = TextStyle(fontSize: 15);
  final textStyleSmall = TextStyle(fontSize: 12);

  CarStatusWidget({super.key, required this.car, required this.carStatus});

  @override
  Widget build(BuildContext context) {
    return ConstrainedBox(
      constraints: BoxConstraints(minWidth: 150),
      child: Padding(
        padding: const EdgeInsets.all(10.0),
        child: Row(
          children: [
            Expanded(
              child: Column(
                children: [
                  if (car.name == CarType.id4.name)
                    Image.asset('assets/images/ID4.png')
                  else
                    Image.asset('assets/images/${car.name}.jpg'),
                  Text(
                    carStatus != null
                        ? dateTimeFormatter.format(carStatus!.lastUpdate)
                        : "--.--.-- --:--:--",
                    style: textStyleSmall,
                  ),
                  FittedBox(
                    fit: BoxFit.scaleDown,
                    child: Text("${carStatus?.mileage.toString() ?? "-"} km",
                        style: textStyle),
                  ),
                ],
              ),
            ),
            Expanded(
              child: Column(
                children: [
                  Row(
                    children: [
                      SvgPicture.asset('assets/images/connected.svg'),
                      SvgPicture.asset('assets/images/checked.svg'),
                    ],
                  ),
                  Row(
                    children: [
                      Expanded(
                        child: Column(
                          children: [
                            FittedBox(
                              fit: BoxFit.scaleDown,
                              child: Text(
                                "${carStatus?.battery ?? 0} % / ${carStatus?.chargingTarget ?? 0} %",
                                style: textStyle,
                              ),
                            ),
                            LinearProgressIndicator(
                              minHeight: 6.0,
                              value: (carStatus?.battery ?? 0) / 100,
                              backgroundColor: Colors.grey,
                              borderRadius: BorderRadius.all(Radius.circular(3)),
                              valueColor: AlwaysStoppedAnimation<Color>(
                                  Color.fromARGB(255, 78, 135, 82)),
                            ),
                            if (carStatus?.chargingEndTime != null)
                              FittedBox(
                                fit: BoxFit.scaleDown,
                                child: Text(
                                  "Bis: ${carStatus != null ? timeFormatter.format(carStatus!.chargingEndTime!.toLocal()) : "-"}",
                                  style: textStyle,
                                ),
                              ),
                            FittedBox(
                              fit: BoxFit.scaleDown,
                              child: Text(
                                "${carStatus?.remainingRange ?? 0} km",
                                style: textStyle,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  )
                ],
              ),
            )
          ],
        ),
      ),
    );
  }
}
