using Games.Reefscape.Enums;
using Games.Reefscape.GamePieceSystem;
using Games.Reefscape.Robots;
using MoSimCore.BaseClasses.GameManagement;
using MoSimCore.Enums;
using RobotFramework.Components;
using RobotFramework.Controllers.GamePieceSystem;
using RobotFramework.Controllers.PidSystems;
using RobotFramework.Enums;
using RobotFramework.GamePieceSystem;
using System.Collections;
using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods._604B
{
    public class Quixshot: ReefscapeRobotBase
    {
        [Header("Components")]
        [SerializeField] private GenericJoint shooterPivot;
        [SerializeField] private GenericJoint intakePivot;
        [SerializeField] private GenericElevator intakeDeploy;
        [SerializeField] private GenericRoller leftIntakeRollerJoint;
        [SerializeField] private GenericRoller rightIntakeRollerJoint;
        [SerializeField] private Transform leftIntakeSensor;
        [SerializeField] private Transform rightIntakeSensor;

        [Header("Animation Joints (Wheels)")]
        [SerializeField] private GenericAnimationJoint[] intakeWheels;
        [SerializeField] private GenericAnimationJoint[] shooterWheels;
        [SerializeField] private float wheelIntakeSpeed = 500f;
        [SerializeField] private float wheelShooterSpeed = 500f;

        private bool _isScoring = false;

        [Header("PIDS")]
        [SerializeField] private PidConstants shooterPid;
        [SerializeField] private PidConstants intakePid;

        [Header("coral Setpoints")]
        [SerializeField] private QuixshotSetpoint stowSetpoint;
        [SerializeField] private QuixshotSetpoint intakeSetpoint;
        [SerializeField] private QuixshotSetpoint handoffSetpoint;
        [SerializeField] private QuixshotSetpoint l1Setpoint;
        [SerializeField] private QuixshotSetpoint l2Setpoint;
        [SerializeField] private QuixshotSetpoint l3Setpoint;
        [SerializeField] private QuixshotSetpoint l4Setpoint;

        private ReefscapeSetpoints _previousSetpoint = ReefscapeSetpoints.Stow;

        [Header("Intake Components")]
        [SerializeField] private ReefscapeGamePieceIntake coralIntake;

        [Header("Game Piece States")]
        [SerializeField] private string currentState;
        [SerializeField] private GamePieceState coralIntakeState;
        [SerializeField] private GamePieceState coralStowState;

        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _coralController;

        [Header("Target Setpoints")]
        [SerializeField] private float targetShooterAngle;
        [SerializeField] private float targetIntakeAngle;
        [SerializeField] private float targetIntakeDistance;
        [SerializeField] private float targetShootingForce;

        protected override void Start()
        {
            base.Start();
    
            intakePivot.SetPid(intakePid);
            shooterPivot.SetPid(shooterPid);

            targetShooterAngle = stowSetpoint.shooterPivotAngle;
            targetIntakeAngle = stowSetpoint.intakePivotAngle;
            targetIntakeDistance = stowSetpoint.intakeDeployDistance;
    
            RobotGamePieceController.SetPreload(coralStowState);

            _coralController = RobotGamePieceController.GetPieceByName(ReefscapeGamePieceType.Coral.ToString());
            
            _coralController.gamePieceStates = new[]
            {
                coralIntakeState, coralStowState
            };
            _coralController.intakes.Add(coralIntake);
        }

        private void LateUpdate()
        {
            intakePivot.UpdatePid(intakePid);
            shooterPivot.UpdatePid(shooterPid);
        }

        private void FixedUpdate()
        {
            intakePivot.SetTargetAngle(targetIntakeAngle);
            shooterPivot.SetTargetAngle(targetShooterAngle);
            intakeDeploy.SetTarget(targetIntakeDistance);

            var canIntake = _coralController.currentStateNum == 0 && IntakeAction.IsPressed() && _coralController.currentStateNum == 0;

            var readState = _coralController.GetCurrentState();
            if (readState != null)
            {
                currentState = readState.name;
            }

            if (BaseGameManager.Instance.RobotState == RobotState.Disabled) return;

            if (!_isScoring)
            {
                bool isIntaking = (CurrentSetpoint == ReefscapeSetpoints.Intake || CurrentSetpoint == ReefscapeSetpoints.RobotSpecial || CurrentSetpoint == ReefscapeSetpoints.Stack) && IntakeAction.IsPressed();

                if (isIntaking)
                {
                    foreach (var wheel in intakeWheels)
                    {
                        wheel.VelocityRoller(wheelIntakeSpeed).useAxis(JointAxis.Z);
                    }
                }
                else
                {
                    leftIntakeRollerJoint.ChangeAngularVelocity(0);
                    rightIntakeRollerJoint.ChangeAngularVelocity(0);

                    foreach (var wheel in intakeWheels)
                    {
                        wheel.VelocityRoller(0).useAxis(JointAxis.Z);
                    }
                }
            }

            switch (CurrentSetpoint)
            {
                case ReefscapeSetpoints.Stow:
                    SetSetpoint(stowSetpoint);
                    _coralController.SetTargetState(coralStowState);
                    break;
                case ReefscapeSetpoints.Intake:
                    SetSetpoint(intakeSetpoint);
                    _coralController.SetTargetState(coralIntakeState);
                    _coralController.RequestIntake(coralIntake, canIntake);
                    break;
                case ReefscapeSetpoints.Place:
                    StartCoroutine(PlacePiece(LastSetpoint, readState));
                    break;
                case ReefscapeSetpoints.L1:
                    SetSetpoint(l1Setpoint);
                    break;
                case ReefscapeSetpoints.L2:
                    SetSetpoint(l2Setpoint);
                    break;
                case ReefscapeSetpoints.L3:
                    SetSetpoint(l3Setpoint);
                    break;
                case ReefscapeSetpoints.L4:
                    SetSetpoint(l4Setpoint);
                    break;
            }
            
            _coralController.MoveIntake(coralIntake, coralIntakeState.stateTarget);
            if (!leftIntakeRollerJoint.gameObject.activeSelf)
            {
                leftIntakeRollerJoint.gameObject.SetActive(true);
                rightIntakeRollerJoint.gameObject.SetActive(true);
            }

            _previousSetpoint = CurrentSetpoint;
        }

        private IEnumerator PlacePiece(ReefscapeSetpoints lastSetpoint, GamePieceState readState)
        {
            _isScoring = true;

            float speed = wheelIntakeSpeed;

            foreach (var wheel in shooterWheels)
            {
                wheel.VelocityRoller(speed).useAxis(JointAxis.Y);
            }

            Vector3 force;
            float releaseForce = 0.05f;
            switch (lastSetpoint)
            {
                case ReefscapeSetpoints.L1:
                    releaseForce = l1Setpoint.releaseForce;
                    break;
                case ReefscapeSetpoints.L2:
                    releaseForce = l2Setpoint.releaseForce;
                    break;
                case ReefscapeSetpoints.L3:
                    releaseForce = l3Setpoint.releaseForce;
                    break;
                case ReefscapeSetpoints.L4:
                    releaseForce = l4Setpoint.releaseForce;
                    break;
            }
            force = Vector3.forward * releaseForce;

            _coralController.ReleaseGamePieceWithForce(force);

            // Wait until game pieces are released (state becomes 0) or timeout after 0.5s
            float timer = 0f;
            while ((_coralController.currentStateNum != 0) && timer < 0.5f)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            // Explicitly stop wheels
            foreach (var wheel in shooterWheels)
                wheel.VelocityRoller(0).useAxis(JointAxis.X);

            _isScoring = false; // Release lock
        }

        private void SetSetpoint(QuixshotSetpoint setpoint)
        {
            targetShootingForce = setpoint.releaseForce;
            targetShooterAngle = setpoint.shooterPivotAngle;
            targetIntakeAngle = setpoint.intakePivotAngle;
            targetIntakeDistance = setpoint.intakeDeployDistance;
        }
    }
}