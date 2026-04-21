import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TaskItem } from '../ui.types';

@Component({
  selector: 'stb-task-list-card',
  templateUrl: './task-list-card.component.html',
  styleUrls: ['./task-list-card.component.scss']
})
export class TaskListCardComponent {
  @Input() title = 'Tasks';
  @Input() icon?: string;
  @Input() tasks: TaskItem[] = [];
  @Input() filters: string[] = [];
  @Input() activeFilter?: string;
  @Input() emptyMessage = 'Nothing here yet.';

  @Output() toggle = new EventEmitter<TaskItem>();
  @Output() filterChange = new EventEmitter<string>();

  trackById(_i: number, t: TaskItem) { return t.id; }
}
