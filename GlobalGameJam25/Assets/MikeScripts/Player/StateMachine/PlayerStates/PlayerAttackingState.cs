using UnityEngine;
using UnityEngine.UIElements;



public class PlayerAttackingState : PlayerBaseState
{
    public override void EnterState(PlayerStateManager player)
    {
        Debug.Log("im Attacking!");

        // Deduct mana and hide a shell
        if (player.currentShells > 0)
        {
            player.orbitingShells[player.currentShells - 1].GetComponent<MeshRenderer>().enabled = false;
            player.currentShells -= 1;
            FireProjectile(player);
            UseMana(player.manaCost, player);
        }

        // Transition back if no movement or out of shells
        if (player.movement.magnitude < 0.1 && player.currentShells <= 0)
        {
            player.SwitchState(player.idleState);
        }
        else if (player.currentShells <= 0)
        {
            player.SwitchState(player.walkState);
        }
        //else
    }

    public override void UpdateState(PlayerStateManager player)
    {
        if (Input.GetMouseButtonDown((int)MouseButton.LeftMouse) && player.currentMana > player.manaCost)
        {
            if (player.currentShells > 0)
            {
                player.orbitingShells[player.currentShells - 1].GetComponent<MeshRenderer>().enabled = false;
                player.currentShells -= 1;
                FireProjectile(player);
                UseMana(player.manaCost, player);
            }
        }

        // Transition back if no movement or out of shells
        if (player.movement.magnitude < 0.1 || player.currentShells <= 0)
        {
            player.SwitchState(player.idleState);
        }
        else
        {

        }
    }
    void FireProjectile(PlayerStateManager player)
    {
        // Instantiate the projectile at the firePoint
        if (player.projectilePrefab != null && player.firePoint != null)
        {
            GameObject projectile = GameObject.Instantiate(player.projectilePrefab, player.firePoint.position, player.firePoint.rotation);
        }
    }
    public void UseMana(int mCost, PlayerStateManager player)
    {
        player.currentMana -= mCost;
        player.currentMana = Mathf.Clamp(player.currentMana, 0, player.maxMana);
    }
    public void OnTriggerEnter(Collider other)
    {
        throw new System.NotImplementedException();
    }

    public void OnTriggerExit(Collider other)
    {
        throw new System.NotImplementedException();
    }

    public void OnTriggerStay(Collider other)
    {
        throw new System.NotImplementedException();
    }


}
