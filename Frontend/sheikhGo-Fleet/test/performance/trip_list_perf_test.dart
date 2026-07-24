import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sheikh_go_driver/features/trips/domain/trip_model.dart';

/// Lightweight performance guard: building a large trip list should stay fast.
void main() {
  testWidgets('trip list of 200 items builds under budget', (tester) async {
    final trips = List.generate(
      200,
      (i) => Trip(
        id: i,
        bookingNumber: 'B-$i',
        customerName: 'Customer $i',
        routeName: 'Route $i',
        pickupTime: DateTime.utc(2026, 7, 1).add(Duration(hours: i)),
        status: 2,
        statusName: 'Confirmed',
        totalAmount: 1000 + i.toDouble(),
        lifecycleStatus: 2,
        lifecycleStatusName: 'Scheduled',
        nextActions: const ['Accept'],
      ),
    );

    final sw = Stopwatch()..start();
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ListView.builder(
            itemCount: trips.length,
            itemBuilder: (_, i) {
              final t = trips[i];
              return ListTile(
                title: Text(t.bookingNumber),
                subtitle: Text(t.routeName),
                trailing: Text(t.totalAmount.toStringAsFixed(0)),
              );
            },
          ),
        ),
      ),
    );
    await tester.pump();
    sw.stop();

    expect(sw.elapsedMilliseconds, lessThan(2000),
        reason: 'List of 200 trips should build in under 2s');

    await tester.fling(find.byType(ListView), const Offset(0, -800), 3000);
    await tester.pumpAndSettle();
    expect(find.text('B-0'), findsNothing);
  });
}
