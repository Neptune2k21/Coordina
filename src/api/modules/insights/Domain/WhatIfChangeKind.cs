namespace Coordina.Api.Modules.Insights.Domain;

/// <summary>A change that can be tried on a board without saving it.</summary>
public enum WhatIfChangeKind
{
  SetSize,
  Assign,
  AddDependency,
  RemoveDependency,
  SetTarget,
  Complete,
  AddPeople
}
