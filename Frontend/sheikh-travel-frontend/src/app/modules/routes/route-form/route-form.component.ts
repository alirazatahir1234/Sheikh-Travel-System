import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { RouteService } from '../../../core/services/route.service';

@Component({
  selector: 'app-route-form',
  templateUrl: './route-form.component.html',
  styleUrls: ['./route-form.component.scss']
})
export class RouteFormComponent implements OnInit {
  form: FormGroup;
  loading = false;
  isEdit = false;
  routeId: number | null = null;

  constructor(
    private fb: FormBuilder,
    private routeService: RouteService,
    private router: Router,
    private route: ActivatedRoute,
    private snackBar: MatSnackBar
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      origin: ['', [Validators.required, Validators.maxLength(100)]],
      destination: ['', [Validators.required, Validators.maxLength(100)]],
      distanceKm: [null, [Validators.required, Validators.min(0.1)]],
      estimatedMinutes: [null, [Validators.required, Validators.min(1)]]
    });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit = true;
      this.routeId = +id;
      this.routeService.getById(this.routeId).subscribe(r => this.form.patchValue(r));
    }
  }

  submit(): void {
    if (this.form.invalid) return;
    this.loading = true;
    const req = this.form.value;
    const obs = this.isEdit
      ? this.routeService.update({ ...req, id: this.routeId! })
      : this.routeService.create(req);

    obs.subscribe({
      next: () => {
        this.snackBar.open(`Route ${this.isEdit ? 'updated' : 'created'}`, 'Close', { duration: 2000 });
        this.router.navigate(['/routes']);
      },
      error: () => { this.loading = false; this.snackBar.open('Operation failed', 'Close', { duration: 3000 }); }
    });
  }
}
