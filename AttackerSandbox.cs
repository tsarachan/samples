/// <summary>
/// This is a base class for all Attackers. It includes all of the Attackers' "verbs"--everything they can do.
/// 
/// All Attackers inherit from this.
/// </summary>
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
	private const string NO_DMG_PARTICLE_OBJ = "No damage particle";


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
	protected virtual void UnregisterForEvents(){
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

		//if the attacker can't move the entirety of their speed, but could move a shorter distance, allow that
		//don't go below 1; the rest of the movement system handles that
		if (attemptedMove > 1){
			while (attemptedMove > 1){
				if ((Services.Board.GeneralSpaceQuery(XPos, ZPos - attemptedMove) != SpaceBehavior.ContentType.None && //blocked by something in the space
					 Services.Board.GeneralSpaceQuery(XPos, ZPos - attemptedMove) != SpaceBehavior.ContentType.Defender) ||
					(ZPos - attemptedMove <= Services.Board.WallZPos && //blocked by the wall
					 Services.Board.GetWallDurability(XPos) > 0) ||
					(ZPos - attemptedMove == 0 && //want to move to last row, but there's a defender holding that position
					 Services.Board.GeneralSpaceQuery(XPos, 0) == SpaceBehavior.ContentType.Defender) ||
					(Services.Board.GeneralSpaceQuery(XPos, ZPos - attemptedMove) == SpaceBehavior.ContentType.Defender && //defender is blocked from being pushed
					 Services.Board.GeneralSpaceQuery(XPos, ZPos - attemptedMove - 1) != SpaceBehavior.ContentType.None) ||
					(Services.Board.GetSpace(XPos, ZPos - attemptedMove).Block)) { //the space is blocked for movement by, e.g., a rockfall
					attemptedMove--;
				}
				else break;
			}
		}


		//if the space the attacker wants to move to is empty, go there.
		//this moves by spaces in the grid; MoveTask is responsible for having grid positions turned into world coordinates
		if (Services.Board.GeneralSpaceQuery(XPos, ZPos - attemptedMove) == SpaceBehavior.ContentType.None ||
			Services.Board.GeneralSpaceQuery(XPos, ZPos - attemptedMove) == SpaceBehavior.ContentType.Defender){
				

			//is this enemy trying to move through the wall? If so, block the move.
			if (ZPos - attemptedMove == Services.Board.WallZPos){
				if (Services.Board.GetWallDurability(XPos) > 0) return;
			}

			//last check; if trying to take the last step to the last row, and there's a defender there, block the move
			if (ZPos - attemptedMove == 0 &&
				Services.Board.GeneralSpaceQuery(XPos, ZPos - attemptedMove) == SpaceBehavior.ContentType.Defender) return;


			//if this attacker is pushing a defender back, move the defender
			//while the attacker is at it, also push any tankards back so that they end up, if possible,
			//in a space where they can be used

			int temp = attemptedMove;

			while (temp > 0){
				if (Services.Board.GeneralSpaceQuery(XPos, ZPos - temp) == SpaceBehavior.ContentType.Defender){

					if (ZPos - attemptedMove - 1 >= 0){ //don't try to push defenders back off the board
						GameObject defender = Services.Board.GetThingInSpace(XPos, ZPos - temp);
						Services.Board.TakeThingFromSpace(XPos, ZPos - temp);
						Services.Board.PutThingInSpace(defender, XPos, ZPos - attemptedMove - 1, SpaceBehavior.ContentType.Defender);


						defender.GetComponent<DefenderSandbox>().NewLoc(XPos, ZPos - attemptedMove - 1);
						Services.Tasks.AddTask(new MoveDefenderTask(defender.GetComponent<Rigidbody>(),
											   defender.GetComponent<DefenderSandbox>().Speed,
											   new System.Collections.Generic.List<TwoDLoc>() { new TwoDLoc(XPos, ZPos - temp),
											   new TwoDLoc(XPos, ZPos - attemptedMove - 1)}));
					}
				}

				if (Services.Board.CheckIfTankard(XPos, ZPos - temp) == true){
					if (ZPos - attemptedMove - 1 >= 0){ //don't try to push tankards back off the board, either
						Services.Board.GetSpace(XPos, ZPos - temp).Tankard = false;
						Services.Board.GetSpace(XPos, ZPos - attemptedMove - 1).Tankard = true;

						Transform localTankard = Services.Board.GetTankardInSpace(new TwoDLoc(XPos, ZPos - temp));
						Debug.Assert(localTankard != null, "Didn't find local tankard.");

						Services.Tasks.AddTask(new MoveObjectTask(localTankard,
																  new TwoDLoc(XPos, ZPos - temp),
																  new TwoDLoc(XPos, ZPos - attemptedMove - 1)));
						localTankard.GetComponent<TankardBehavior>().GridLoc = new TwoDLoc(XPos, ZPos - attemptedMove - 1);
					}
				}

				temp--;
			}

			//OK, not moving through a wall and any defender is out of the way.
			//Leave the current space, go into the new space, move on-screen, and update this attacker's
			//understanding of its own position
			Services.Board.TakeThingFromSpace(XPos, ZPos);
			Services.Board.PutThingInSpace(gameObject, XPos, ZPos - attemptedMove, SpaceBehavior.ContentType.Attacker);
			Services.Tasks.AddTask(new MoveTask(transform, XPos, ZPos - attemptedMove, Services.Attackers.MoveSpeed));
			NewLoc(XPos, ZPos - attemptedMove);
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
			Services.Tasks.AddTask(new MoveTask(transform, XPos + dir, ZPos, Services.Attackers.MoveSpeed));
			NewLoc(XPos + dir, ZPos);
			return true;
		} else return false;
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
