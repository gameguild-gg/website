'use client';

import {useFormState} from 'react-dom';

import {CreateProjectFormState} from "@/lib/project/create-project-form-state";
import {createProject} from "@/lib/project/create-project.action";

const initialState: CreateProjectFormState = {};

export function useCreateProjectFormState() {
  return useFormState(createProject, initialState);
}