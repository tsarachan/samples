/// <summary>
/// This is a base class for all Attackers. It includes all of the Attackers' "verbs"--everything they can do.
/// 
/// All Attackers inherit from this.
/// </summary>
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AttackerSandbox : MonoBehaviour {

	/////////////////////////////////////////////
	/// Fields
	/////////////////////////////////////////////

	//position in the grid and speed
	public int XPos { get; set; }
	public int ZPos { get; set; }
	[SerializeField] protected int speed = 1; //speed in spaces/move, not 3D world speed
	protected int currentSpeed = 0;

	//attacker stats
	public int AttackMod { get; set; }
	public int Armor { get; set; }
	public int Health { get; set; }
	protected int baseHealth = 1;


	//has this attacker already fought this turn?
	public bool FoughtThisTurn { get; set; }


	//how much damage does this attacker do to walls?
	public int SiegeStrength { get; private set; }
	[SerializeField] int startSiegeStrength = 1;


	//did this attacker just spawn? If so, it won't move this turn
	public bool SpawnedThisTurn { get; set; }


	//is something stopping this attacker from moving?
	public bool Blocked { get; set; }


	//left and right; used for being lured to the side
	protected const int WEST = -1;
	protected const int EAST = 1;


	//for the UI, when the player needs info on this attacker
	protected string attackerName = "Skeleton";
	protected const string ATTACK = "Attack: ";
	protected const string ARMOR = "Armor: ";
	protected const string HEALTH = "Health: ";
	protected const string NEWLINE = "\n";


	//things that make this attacker go fast have this in their name
	protected string FAST_MARKER = "Fast";


	//feedback for attacks that do no damage
	protected ParticleSystem noDamageParticle;
	protected const string NO_DMG_PARTICLE_OBJ = "No damage particle";


	//tasks that move pieces
	protected List<MoveDefenderTask> pushTasks = new List<MoveDefenderTask>();
	protected List<MoveTask> moveTasks = new List<MoveTask>();


	//access to the mini's animation, for those attackers that need to control it via code
	protected const string MODEL_ORGANIZER = "Model";
	protected const string MINI_OBJ = "Miniature";







	/////////////////////////////////////////////
	/// Functions
	/////////////////////////////////////////////


	//initialize variables
	public virtual void Setup(){
		FoughtThisTurn = false;
		SiegeStrength = startSiegeStrength;
		SpawnedThisTurn = true;
		AttackMod = 0;
		Armor = 0;
		Health = baseHealth;
		Blocked = false;
		noDamageParticle = transform.Find(NO_DMG_PARTICLE_OBJ).GetComponent<ParticleSystem>();

		RegisterForEvents();
	}


	#region events


	/// <summary>
	/// Call this to register for all events this attacker cares about.
	/// </summary>
	protected virtual void RegisterForEvents(){
		Services.Events.Register<BlockColumnEvent>(BecomeBlocked);
		Services.Events.Register<UnblockColumnEvent>(BecomeUnblocked);
	}


	/// <summary>
	/// Movement is blocked when this attacker receives an event indicating that its column cannot move.
	/// </summary>
	/// <param name="e">The BlockColumn event.</param>
	protected void BecomeBlocked(Event e){
		BlockColumnEvent blockEvent = e as BlockColumnEvent;

		if (blockEvent.Column == XPos) Blocked = true;
	}


	/// <summary>
	/// Movement is unblocked when this attacker receives a relevant event.
	/// </summary>
	/// <param name="e">The UnblockColumn event.</param>
	protected void BecomeUnblocked(Event e){
		UnblockColumnEvent unblockEvent = e as UnblockColumnEvent;

		if (unblockEvent.Column == XPos) Blocked = false;
	}


	/// <summary>
	/// Call this when the attacker is being taken off the board to unregister.
	/// </summary>
	public virtual void UnregisterForEvents(){
		Services.Events.Unregister<BlockColumnEvent>(BecomeBlocked);
		Services.Events.Unregister<UnblockColumnEvent>(BecomeBlocked);
	}


	#endregion events


	//set this attacker's position
	public void NewLoc(int x, int z){
		XPos = x;
		ZPos = z;
	}


	#region movement


	/// <summary>
	/// Anything this attacker needs to do at the start of the movement phase happens here.
	/// </summary>
	public virtual void PrepareToMove(){
		currentSpeed = GetSpeed();
		moveTasks.Clear();
		pushTasks.Clear();
	}


	/// <summary>
	/// This function manages limits and controls on movement, deciding whether and where the attacker should move.
	/// </summary>
	public void TryMove(){
		//don't move if this attacker spawned this turn, but get ready to move next turn
		if (SpawnedThisTurn){
			SpawnedThisTurn = false;
			return;
		}

		//don't move if the attacker is blocked by an "off the board" game effect.
		if (Blocked){
			return;
		}
			
		//move west or east if being lured there and there is space to do so
		//note that this privileges westward movement; it checks westward movement first, and will therefore go west in preference to being lured east
		if (Services.Board.CheckIfLure(XPos + WEST, ZPos)){
			if (TryMoveLateral(WEST)) return;
		} else if (Services.Board.CheckIfLure(XPos + EAST, ZPos)){
			if (TryMoveLateral(EAST)) return;
		}

		//move west or east if blocked from moving forward
		//note that this privileges westward movement; it checks westward movement first, and will therefore go west in preference to being lured east
		if (Services.Board.CheckIfBlock(XPos, ZPos - 1)){
			Services.Events.Fire(new BlockedEvent());

			if (TryMoveLateral(WEST)) return;
			else if (TryMoveLateral(EAST)) return;

			return; //if this attacker can't move laterally around the block, it can't move at all
		}


		//if the attacker gets this far, it can make a normal move to the south.
		TryMoveSouth();
	}


	/// <summary>
	/// Move this attacker south a number of spaces based on their speed.
	/// </summary>
	protected void TryMoveSouth(){
		int attemptedMove = currentSpeed;

		//sanity check; prevent this attacker from trying to move off the board
		if (ZPos - attemptedMove < 0) attemptedMove = ZPos;

		//don't try to move past the wall
		if (ZPos - attemptedMove <= Services.Board.WallZPos){ //if trying to move south of the wall . . .
			if (Services.Board.GetWallDurability(XPos) > 0){ //and the wall is standing . . .
				if (ZPos >= Services.Board.WallZPos){ //and the attacker is north of the wall . . .

					//calculate an attemptedMove that goes up to the wall, but stops there
					attemptedMove = ZPos - (Services.Board.WallZPos + 1); //+1 because the attacker must stop at the space before the wall
		
					Debug.Assert(attemptedMove >= 0);
				}
			}
		}
		 
		while (attemptedMove > 0){
			//if something immobile is in the way, stop
			if (Services.Board.GeneralSpaceQuery(XPos, ZPos - 1) == SpaceBehavior.ContentType.Attacker ||
				Services.Board.CheckIfBlock(XPos, ZPos - 1)){
				attemptedMove = 0;
			}
			//if nothing's in the way, move
			else if (Services.Board.GeneralSpaceQuery(XPos, ZPos - 1) == SpaceBehavior.ContentType.None){
				GoToSouth(1);
				MoveTankard();
				attemptedMove--;
			}

			//if a defender is in the way, see if the attacker can push them back
			else if (Services.Board.GeneralSpaceQuery(XPos, ZPos - 1) == SpaceBehavior.ContentType.Defender){


				//if the attacker is trying to move to the last space, and there's a defender there, the attacker is stuck
				if (ZPos - 1 == 0){
					attemptedMove = 0;
				}

				//if there's something behind the defender, they can't be pushed
				else if (Services.Board.GeneralSpaceQuery(XPos, ZPos - 2) != SpaceBehavior.ContentType.None){
					attemptedMove = 0;
				}

				//at this point the defender is pushable; do so
				else {
					PushDefender(Services.Board.GetThingInSpace(XPos, ZPos - 1));
					GoToSouth(1);
					MoveTankard();
					attemptedMove--;
				}
			}
		}


		if (moveTasks.Count > 0){
			for (int i = 0; i < moveTasks.Count - 1; i++){
				moveTasks[i].Then(moveTasks[i + 1]);
			}
			Services.Tasks.AddOrderedTask(moveTasks[0]);
		}


		if (pushTasks.Count > 0){
			for (int i = 0; i < pushTasks.Count - 1; i++){
				pushTasks[i].Then(pushTasks[i + 1]);
			}

			Services.Tasks.AddOrderedTask(pushTasks[0]);
		}
	}


	/// <summary>
	/// Determine the attacker's current speed, based on whether there are things adjacent to it that cause it to move faster.
	/// </summary>
	/// <returns>The speed, in grid spaces.</returns>
	protected virtual int GetSpeed(){
		int temp = speed;

		temp += Services.Momentum.Momentum;

		if (LookForSpeedUp(XPos, ZPos + 1)) temp++;
		if (LookForSpeedUp(XPos, ZPos - 1)) temp++;
		if (LookForSpeedUp(XPos - 1, ZPos)) temp++;
		if (LookForSpeedUp(XPos + 1, ZPos)) temp++;

		return temp;
	}


	/// <summary>
	/// Determine whether there's something in a space that causes an attacker to move faster.
	/// </summary>
	/// <returns><c>true</c> if there is such a thing in the space, <c>false</c> otherwise.</returns>
	/// <param name="x">The x coordinate of the grid space to check.</param>
	/// <param name="z">The z coordinate of the grid space to check.</param>
	protected virtual bool LookForSpeedUp(int x, int z){

		//first, if there's no space at the given coordinates, there's nothing to speed the attacker up there
		if (x >= BoardBehavior.BOARD_WIDTH ||
			x < 0 ||
			z >= BoardBehavior.BOARD_HEIGHT ||
			z < 0) return false;


		//if there's an attacker in the space, return true if it's got "Fast" in its name, false otherwise
		if (Services.Board.GeneralSpaceQuery(x, z) == SpaceBehavior.ContentType.Attacker){
			return (Services.Board.GetThingInSpace(x, z).name.Contains(FAST_MARKER)) ? true : false;
		}

		//by default, return false
		return false;
	}


	/// <summary>
	/// Try to move the attacker one space east or west.
	/// </summary>
	/// <returns><c>true</c> if the attacker was able to move into an empty space, <c>false</c> if the space was occupied, blocking movement.</returns>
	/// <param name="dir">The direction of movement, east (1) or west (-1).</param>
	protected bool TryMoveLateral(int dir){
		//if the space one to the east is empty, go there.
		if (Services.Board.GeneralSpaceQuery(XPos + dir, ZPos) == SpaceBehavior.ContentType.None){
			Services.Board.TakeThingFromSpace(XPos, ZPos);
			Services.Board.PutThingInSpace(gameObject, XPos + dir, ZPos, SpaceBehavior.ContentType.Attacker);
			Services.Tasks.AddOrderedTask(new MoveTask(transform, XPos + dir, ZPos, Services.Attackers.MoveSpeed));
			NewLoc(XPos + dir, ZPos);
			Services.Rulebook.IncreaseAdvanceDuration();
			return true;
		} else return false;
	}


	private void GoToSouth(int speed){
		Services.Board.TakeThingFromSpace(XPos, ZPos);
		Services.Board.PutThingInSpace(gameObject, XPos, ZPos - speed, SpaceBehavior.ContentType.Attacker);
		moveTasks.Add(new MoveTask(transform, XPos, ZPos - speed, Services.Attackers.MoveSpeed));
		NewLoc(XPos, ZPos - speed);
		Services.Rulebook.IncreaseAdvanceDuration();
	}


	private void MoveTankard(){
		if (ZPos == 0) return; //don't try to push tankards off the screen

		if (Services.Board.CheckIfTankard(XPos, ZPos)){
			if (!Services.Board.CheckIfTankard(XPos, ZPos - 1)){
				Services.Board.GetSpace(XPos, ZPos).Tankard = false;
				Services.Board.GetSpace(XPos, ZPos - 1).Tankard = true;

				Transform localTankard = Services.Board.GetTankardInSpace(new TwoDLoc(XPos, ZPos));
				Debug.Assert(localTankard != null, "Didn't find local tankard.");
		
				Services.Tasks.AddTask(new MoveObjectTask(localTankard,
														  new TwoDLoc(XPos, ZPos),
														  new TwoDLoc(XPos, ZPos - 1)));
				localTankard.GetComponent<TankardBehavior>().GridLoc = new TwoDLoc(XPos, ZPos - 1);
			}
		}
	}


	private void PushDefender(GameObject defender){
		Debug.Assert(defender.tag == "Defender", "Trying to push something that's not a defender: " + defender.name);

		Services.Board.TakeThingFromSpace(XPos, ZPos - 1);
		Services.Board.PutThingInSpace(defender, XPos, ZPos - 2, SpaceBehavior.ContentType.Defender);


		defender.GetComponent<DefenderSandbox>().NewLoc(XPos, ZPos - 2);

		pushTasks.Add(new MoveDefenderTask(defender.GetComponent<Rigidbody>(),
										   Services.Attackers.MoveSpeed,
										   new System.Collections.Generic.List<TwoDLoc>() { new TwoDLoc(XPos, ZPos - 1),
										   new TwoDLoc(XPos, ZPos - 2)}));
	}



	#endregion movement


	/// <summary>
	/// A publicly-accessible way to find out what column this attacker is in.
	/// 
	/// Used for, frex., besieging the corerct wall.
	/// </summary>
	/// <returns>The column, zero-indexed.</returns>
	public int GetColumn(){
		return XPos;
	}


	#region combat


	/// <summary>
	/// Call this when an attacker suffers damage.
	/// </summary>
	/// <param name="damage">The amount of damage sustained, after all modifiers.</param>
	public virtual void TakeDamage(int damage){
		Health -= damage;

		if (Health <= 0) {
			BeDefeated();
		}
	}


	/// <summary>
	/// Call this when this attacker is defeated by a defender.
	/// </summary>
	public virtual void BeDefeated(){
		Services.Attackers.EliminateAttacker(this);
		Services.Board.TakeThingFromSpace(XPos, ZPos);
		Services.Events.Fire(new AttackerDefeatedEvent(this));
		UnregisterForEvents();

		AttackerFallTask fallTask = new AttackerFallTask(GetComponent<Rigidbody>());
		EjectAttackerTask throwTask = new EjectAttackerTask(GetComponent<Rigidbody>());
		fallTask.Then(throwTask);
		throwTask.Then(new DestroyAttackerTask(gameObject));
		Services.Tasks.AddTask(fallTask);
	}


	/// <summary>
	/// Call this when the attacker is taken out of the game by something that the defenders aren't rewarded for (e.g., the end of a wave).
	/// </summary>
	public virtual void BeRemovedFromBoard(){
		Services.Board.TakeThingFromSpace(XPos, ZPos);
		EjectAttackerTask throwTask = new EjectAttackerTask(GetComponent<Rigidbody>());
		throwTask.Then(new DestroyAttackerTask(gameObject));
		Services.Tasks.AddTask(throwTask);
	}


	/// <summary>
	/// Call this when the defender does no damage to the attacker, to provide appropriate feedback.
	/// </summary>
	public void FailToDamage(){
		noDamageParticle.Play();
	}


	#endregion combat


	/// <summary>
	/// Provides information on this attacker when the attacker is clicked.
	/// </summary>
	/// <returns>This attacker's name and stats.</returns>
	public string GetUIInfo(){
		return attackerName + NEWLINE +
			   ATTACK + AttackMod.ToString() + NEWLINE +
			   ARMOR + Armor.ToString() + NEWLINE +
			   HEALTH + Health.ToString() + NEWLINE;
	}
}
