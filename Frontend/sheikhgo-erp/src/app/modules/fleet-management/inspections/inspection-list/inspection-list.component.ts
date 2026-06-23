import { Component, OnInit, inject } from '@angular/core';
import { FleetService } from '../../services/fleet.service';
import { InspectionRow } from '../../models/fleet.model';

@Component({
  selector: 'app-inspection-list',
  templateUrl: './inspection-list.component.html',
  styleUrls: ['./inspection-list.component.scss']
})
export class InspectionListComponent implements OnInit {
  private readonly fleet = inject(FleetService);

  inspections: InspectionRow[] = [];
  loading = true;

  ngOnInit(): void {
    this.fleet.getInspections().subscribe({
      next: (rows) => {
        this.inspections = rows;
        this.loading = false;
      },
      error: () => (this.loading = false)
    });
  }
}
