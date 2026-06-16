import { Component, OnInit, inject } from '@angular/core';
import { FleetService } from '../../services/fleet.service';
import { AssignmentRow } from '../../models/fleet.model';

@Component({
  selector: 'app-assignment-board',
  templateUrl: './assignment-board.component.html',
  styleUrls: ['./assignment-board.component.scss']
})
export class AssignmentBoardComponent implements OnInit {
  private readonly fleet = inject(FleetService);

  assignments: AssignmentRow[] = [];
  loading = true;

  ngOnInit(): void {
    this.fleet.getAssignments().subscribe({
      next: (rows) => {
        this.assignments = rows;
        this.loading = false;
      },
      error: () => (this.loading = false)
    });
  }
}
