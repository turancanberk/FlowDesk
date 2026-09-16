"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { TaskStatus } from "@/types/domain";
import {
  changeTaskStatus,
  createTask,
  deleteTask,
  getTask,
  listTasks,
  updateTask,
} from "./task-api";
import type { TaskDetail, TaskFilters, TaskInput } from "./task-types";

export const taskKeys = {
  all: (slug: string) => ["tasks", slug] as const,
  list: (slug: string, filters: TaskFilters) => [...taskKeys.all(slug), "list", filters] as const,
  detail: (slug: string, taskId: string) => [...taskKeys.all(slug), "detail", taskId] as const,
};

export function useTasks(slug: string, filters: TaskFilters) {
  return useQuery({
    queryKey: taskKeys.list(slug, filters),
    queryFn: ({ signal }) => listTasks(slug, filters, signal),
    // Keeps the current page on screen while the next one loads, so the list
    // does not empty on every keystroke.
    placeholderData: keepPreviousData,
  });
}

export function useTask(slug: string, taskId: string) {
  return useQuery({
    queryKey: taskKeys.detail(slug, taskId),
    queryFn: ({ signal }) => getTask(slug, taskId, signal),
    retry: false,
  });
}

export function useCreateTask(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: TaskInput) => createTask(slug, input),
    onSuccess: (task) => writeBack(queryClient, slug, task),
  });
}

export function useUpdateTask(slug: string, taskId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: TaskInput) => updateTask(slug, taskId, input),
    onSuccess: (task) => writeBack(queryClient, slug, task),
  });
}

/**
 * Ticks a task off, or puts it back.
 *
 * Takes the id per call rather than per hook, so one instance serves a whole
 * list of checkboxes instead of one hook per row.
 */
export function useChangeTaskStatus(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { taskId: string; status: TaskStatus }) =>
      changeTaskStatus(slug, input.taskId, input.status),
    onSuccess: (task) => writeBack(queryClient, slug, task),
  });
}

export function useDeleteTask(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (taskId: string) => deleteTask(slug, taskId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: taskKeys.all(slug) });
    },
  });
}

function writeBack(
  queryClient: ReturnType<typeof useQueryClient>,
  slug: string,
  task: TaskDetail,
) {
  queryClient.setQueryData(taskKeys.detail(slug, task.id), task);
  void queryClient.invalidateQueries({ queryKey: taskKeys.all(slug) });
}
